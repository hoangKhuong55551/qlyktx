using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using Qlykytucxa.Models;
using Qlykytucxa.Services;

namespace Qlykytucxa.Controllers
{
    public class AccountController : Controller
    {
        private MiniDormDBEntities db = new MiniDormDBEntities();
        private DormExtContext ext = new DormExtContext();

        private const string LoginFailedMessage = "Tên đăng nhập hoặc mật khẩu không chính xác!";

        // GET: /Account/Login
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View(new LoginViewModel());
        }

        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model, string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string username = (model.Username ?? "").Trim();
            var account = ext.UserAccounts.FirstOrDefault(a => a.Username == username);

            if (account != null && account.LockoutEnd.HasValue && account.LockoutEnd.Value > DateTime.Now)
            {
                int minutes = (int)Math.Ceiling((account.LockoutEnd.Value - DateTime.Now).TotalMinutes);
                ModelState.AddModelError("", $"Tài khoản tạm bị khóa do nhập sai mật khẩu nhiều lần. Vui lòng thử lại sau {minutes} phút.");
                return View(model);
            }

            if (account == null || !AccountService.VerifyPassword(model.Password, account.PasswordHash))
            {
                if (account != null)
                {
                    account.FailedLoginCount++;
                    if (account.FailedLoginCount >= AccountService.MaxFailedLogins)
                    {
                        account.LockoutEnd = DateTime.Now.Add(AccountService.LockoutDuration);
                        account.FailedLoginCount = 0;
                    }
                    ext.SaveChanges();
                }
                ModelState.AddModelError("", LoginFailedMessage);
                return View(model);
            }

            var sessionUser = new UserSession
            {
                AccountId = account.Id,
                Username = account.Username,
                FullName = account.FullName,
                Role = account.Role,
                MustChangePassword = account.MustChangePassword
            };

            if (account.Role == UserRoles.Student)
            {
                var student = account.StudentId.HasValue ? db.Students.Find(account.StudentId.Value) : null;
                if (student == null)
                {
                    ModelState.AddModelError("", LoginFailedMessage);
                    return View(model);
                }

                var activeReg = db.RoomRegistrations.Include(r => r.Rooms)
                    .FirstOrDefault(r => r.StudentId == student.StudentId && r.Status == DormConstants.StatusDangO);
                sessionUser.FullName = student.FullName;
                sessionUser.StudentId = student.StudentId;
                sessionUser.RoomId = activeReg != null ? (int?)activeReg.RoomId : null;
                sessionUser.RoomNumber = activeReg != null && activeReg.Rooms != null ? activeReg.Rooms.RoomNumber : "Chưa xếp";
                Session["StudentId"] = student.StudentId;
                Session["RoomNumber"] = sessionUser.RoomNumber;
            }

            account.FailedLoginCount = 0;
            account.LockoutEnd = null;
            ext.SaveChanges();

            Session["User"] = sessionUser;
            Session["Role"] = sessionUser.Role;
            Session["FullName"] = sessionUser.FullName;

            if (sessionUser.MustChangePassword)
            {
                TempData["Error"] = "Bạn đang dùng mật khẩu mặc định / mật khẩu tạm. Vui lòng đổi mật khẩu trước khi tiếp tục.";
                return RedirectToAction("ChangePassword");
            }

            TempData["Success"] = $"Xin chào {sessionUser.FullName}!";
            if (account.Role == UserRoles.Admin && !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
            return RedirectToRoute(UserSession.HomeRoute(account.Role));
        }

        // GET: /Account/ChangePassword
        public ActionResult ChangePassword()
        {
            ViewBag.Forced = UserSession.FromSession(Session).MustChangePassword;
            return View(new ChangePasswordViewModel());
        }

        // POST: /Account/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangePassword(ChangePasswordViewModel model)
        {
            var user = UserSession.FromSession(Session);
            ViewBag.Forced = user.MustChangePassword;
            var account = ext.UserAccounts.Find(user.AccountId);
            if (account == null)
            {
                Session.Clear();
                return RedirectToAction("Login");
            }

            if (ModelState.IsValid)
            {
                if (!AccountService.VerifyPassword(model.CurrentPassword, account.PasswordHash))
                    ModelState.AddModelError("CurrentPassword", "Mật khẩu hiện tại không đúng!");
                else if (model.NewPassword == model.CurrentPassword)
                    ModelState.AddModelError("NewPassword", "Mật khẩu mới phải khác mật khẩu hiện tại!");
                else if (string.Equals(model.NewPassword, account.Username, StringComparison.OrdinalIgnoreCase)
                         || model.NewPassword == AccountService.DefaultPassword)
                    ModelState.AddModelError("NewPassword", "Mật khẩu quá dễ đoán (trùng tên đăng nhập hoặc mật khẩu mặc định)!");
            }

            if (!ModelState.IsValid) return View(model);

            account.PasswordHash = AccountService.HashPassword(model.NewPassword);
            account.MustChangePassword = false;
            ext.SaveChanges();

            user.MustChangePassword = false;
            TempData["Success"] = "Đổi mật khẩu thành công!";
            return RedirectToRoute(UserSession.HomeRoute(user.Role));
        }

        // GET/POST: /Account/Logout
        [AllowAnonymous]
        public ActionResult Logout()
        {
            Session.Clear();
            Session.Abandon();
            TempData["Success"] = "Bạn đã đăng xuất khỏi hệ thống thành công!";
            return RedirectToAction("Login");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
                ext.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
