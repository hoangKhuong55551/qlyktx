using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using Qlykytucxa.Models;

namespace Qlykytucxa.Controllers
{
    public class AccountController : Controller
    {
        private MiniDormDBEntities db = new MiniDormDBEntities();

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
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string user = (model.Username ?? "").Trim();
            string pass = (model.Password ?? "").Trim();

            // 1. Quản trị viên (Admin)
            if (string.Equals(user, "admin", StringComparison.OrdinalIgnoreCase) && pass == "123456")
            {
                var sessionUser = new UserSession
                {
                    Username = "admin",
                    FullName = "Thầy Trưởng Ban Quản lý KTX",
                    Role = "Admin"
                };
                Session["User"] = sessionUser;
                Session["Role"] = "Admin";
                Session["FullName"] = sessionUser.FullName;

                TempData["Success"] = "Chào mừng Thầy Trưởng Ban Quản lý đăng nhập hệ thống!";
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
                return RedirectToAction("Index", "Home");
            }

            // 2. Kỹ thuật viên / Thợ bảo trì (Staff)
            if (string.Equals(user, "staff", StringComparison.OrdinalIgnoreCase) && pass == "123456")
            {
                var sessionUser = new UserSession
                {
                    Username = "staff",
                    FullName = "Nguyễn Văn Hùng (Kỹ thuật viên KTX)",
                    Role = "Staff"
                };
                Session["User"] = sessionUser;
                Session["Role"] = "Staff";
                Session["FullName"] = sessionUser.FullName;

                TempData["Success"] = "Chào mừng Kỹ thuật viên đăng nhập! Đã chuyển tới danh sách bảo trì.";
                return RedirectToAction("Index", "Incidents");
            }

            // 3. Sinh viên KTX (Student) - Đăng nhập bằng Mã sinh viên (VD: SV001)
            var student = db.Students.FirstOrDefault(s => s.StudentCode.ToLower() == user.ToLower());
            if (student != null && (pass == "123456" || pass.ToLower() == user.ToLower()))
            {
                // Tìm thông tin phòng đang ở
                var activeReg = db.RoomRegistrations.Include(r => r.Rooms)
                    .FirstOrDefault(r => r.StudentId == student.StudentId && r.Status == DormConstants.StatusDangO);

                var sessionUser = new UserSession
                {
                    Username = student.StudentCode,
                    FullName = student.FullName,
                    Role = "Student",
                    StudentId = student.StudentId,
                    RoomId = activeReg != null ? (int?)activeReg.RoomId : null,
                    RoomNumber = activeReg != null && activeReg.Rooms != null ? activeReg.Rooms.RoomNumber : "Chưa xếp"
                };
                Session["User"] = sessionUser;
                Session["Role"] = "Student";
                Session["FullName"] = sessionUser.FullName;
                Session["StudentId"] = student.StudentId;
                Session["RoomNumber"] = sessionUser.RoomNumber;

                TempData["Success"] = $"Xin chào bạn {student.FullName}! Đã đăng nhập vào Cổng thông tin Sinh viên.";
                return RedirectToAction("Index", "StudentPortal");
            }

            ModelState.AddModelError("", "Tên đăng nhập hoặc mật khẩu không chính xác!");
            return View(model);
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
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
