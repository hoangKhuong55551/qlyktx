using System;
using System.Linq;
using System.Web.Mvc;
using Qlykytucxa.Filters;
using Qlykytucxa.Models;
using Qlykytucxa.Services;

namespace Qlykytucxa.Controllers
{
    // Quan ly tai khoan dang nhap (chi Admin).
    // - Tai khoan Admin / Staff: tao, sua, xoa, cap lai mat khau, mo khoa.
    // - Tai khoan Sinh vien: chi cap lai mat khau / mo khoa (tao & xoa theo ho so sinh vien).
    [RoleAuthorize(UserRoles.Admin)]
    public class UserAccountsController : Controller
    {
        private MiniDormDBEntities db = new MiniDormDBEntities();
        private DormExtContext ext = new DormExtContext();

        private static readonly string[] ManagedRoles = { UserRoles.Admin, UserRoles.Staff };

        private int CurrentAccountId
        {
            get { return UserSession.FromSession(Session).AccountId; }
        }

        // GET: UserAccounts
        public ActionResult Index(string searchString, string roleFilter)
        {
            var query = ext.UserAccounts.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                string s = searchString.Trim();
                query = query.Where(a => a.Username.Contains(s) || a.FullName.Contains(s));
            }
            if (!string.IsNullOrEmpty(roleFilter))
            {
                query = query.Where(a => a.Role == roleFilter);
            }

            // Admin -> Staff -> Sinh vien, roi theo ten dang nhap
            var accounts = query.ToList()
                .OrderBy(a => a.Role == UserRoles.Admin ? 0 : a.Role == UserRoles.Staff ? 1 : 2)
                .ThenBy(a => a.Username)
                .ToList();

            ViewBag.SearchString = searchString;
            ViewBag.RoleFilter = roleFilter;
            ViewBag.CurrentAccountId = CurrentAccountId;
            ViewBag.Now = DateTime.Now;
            return View(accounts);
        }

        // GET: UserAccounts/Create
        public ActionResult Create()
        {
            return View(new AccountCreateViewModel { Role = UserRoles.Staff });
        }

        // POST: UserAccounts/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(AccountCreateViewModel model)
        {
            model.Username = (model.Username ?? "").Trim();
            model.FullName = (model.FullName ?? "").Trim();

            if (!ManagedRoles.Contains(model.Role))
                ModelState.AddModelError("Role", "Vai trò không hợp lệ!");
            if (ext.UserAccounts.Any(a => a.Username == model.Username))
                ModelState.AddModelError("Username", "Tên đăng nhập này đã được sử dụng!");
            else if (db.Students.Any(s => s.StudentCode == model.Username))
                ModelState.AddModelError("Username", "Tên đăng nhập trùng với mã một sinh viên, vui lòng chọn tên khác!");

            if (!ModelState.IsValid) return View(model);

            string temp = AccountService.GenerateTemporaryPassword();
            ext.UserAccounts.Add(new UserAccount
            {
                Username = model.Username,
                PasswordHash = AccountService.HashPassword(temp),
                Role = model.Role,
                FullName = model.FullName,
                MustChangePassword = true,
                CreatedAt = DateTime.Now
            });
            ext.SaveChanges();

            TempData["Success"] = $"Đã tạo tài khoản {UserRoles.DisplayName(model.Role).ToLower()} \"{model.Username}\". "
                                + $"Mật khẩu tạm: {temp} (chỉ hiển thị một lần, người dùng phải đổi khi đăng nhập lần đầu).";
            return RedirectToAction("Index");
        }

        // GET: UserAccounts/Edit/5 (chi tai khoan Admin / Staff)
        public ActionResult Edit(int id)
        {
            var account = ext.UserAccounts.Find(id);
            if (account == null) return HttpNotFound();
            if (account.Role == UserRoles.Student)
            {
                TempData["Error"] = "Thông tin tài khoản sinh viên được sửa trong hồ sơ sinh viên!";
                return RedirectToAction("Index");
            }

            return View(new AccountEditViewModel
            {
                Id = account.Id,
                Username = account.Username,
                FullName = account.FullName,
                Role = account.Role
            });
        }

        // POST: UserAccounts/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(AccountEditViewModel model)
        {
            var account = ext.UserAccounts.Find(model.Id);
            if (account == null) return HttpNotFound();
            if (account.Role == UserRoles.Student) return RedirectToAction("Index");

            model.Username = account.Username;
            model.FullName = (model.FullName ?? "").Trim();

            if (!ManagedRoles.Contains(model.Role))
                ModelState.AddModelError("Role", "Vai trò không hợp lệ!");
            else if (account.Role == UserRoles.Admin && model.Role != UserRoles.Admin)
            {
                if (account.Id == CurrentAccountId)
                    ModelState.AddModelError("Role", "Bạn không thể tự hạ quyền của chính mình!");
                else if (IsLastAdmin(account.Id))
                    ModelState.AddModelError("Role", "Đây là quản trị viên cuối cùng, không thể hạ quyền!");
            }

            if (!ModelState.IsValid) return View(model);

            account.FullName = model.FullName;
            account.Role = model.Role;
            ext.SaveChanges();

            // Cap nhat ten hien thi neu sua chinh minh
            if (account.Id == CurrentAccountId)
            {
                UserSession.FromSession(Session).FullName = account.FullName;
                Session["FullName"] = account.FullName;
            }

            TempData["Success"] = $"Đã cập nhật tài khoản \"{account.Username}\".";
            return RedirectToAction("Index");
        }

        // POST: UserAccounts/ResetPassword/5 - cap mat khau tam moi + mo khoa
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetPassword(int id)
        {
            var account = ext.UserAccounts.Find(id);
            if (account == null) return HttpNotFound();
            if (account.Id == CurrentAccountId)
            {
                TempData["Error"] = "Để đổi mật khẩu của chính bạn, hãy dùng nút Đổi mật khẩu ở góc trên.";
                return RedirectToAction("Index");
            }

            string temp = AccountService.GenerateTemporaryPassword();
            account.PasswordHash = AccountService.HashPassword(temp);
            account.MustChangePassword = true;
            account.FailedLoginCount = 0;
            account.LockoutEnd = null;
            ext.SaveChanges();

            TempData["Success"] = $"Đã đặt lại mật khẩu cho \"{account.Username}\". Mật khẩu tạm: {temp} "
                                + "(chỉ hiển thị một lần, người dùng phải đổi khi đăng nhập).";
            return RedirectToAction("Index");
        }

        // POST: UserAccounts/Unlock/5 - mo khoa tai khoan bi khoa do nhap sai mat khau
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Unlock(int id)
        {
            var account = ext.UserAccounts.Find(id);
            if (account == null) return HttpNotFound();

            account.FailedLoginCount = 0;
            account.LockoutEnd = null;
            ext.SaveChanges();

            TempData["Success"] = $"Đã mở khóa tài khoản \"{account.Username}\".";
            return RedirectToAction("Index");
        }

        // POST: UserAccounts/Delete/5 (chi tai khoan Admin / Staff)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var account = ext.UserAccounts.Find(id);
            if (account == null) return HttpNotFound();

            string error = null;
            if (account.Role == UserRoles.Student)
                error = "Tài khoản sinh viên được xóa cùng hồ sơ sinh viên (menu Quản lý Sinh viên).";
            else if (account.Id == CurrentAccountId)
                error = "Bạn không thể xóa tài khoản đang đăng nhập!";
            else if (account.Role == UserRoles.Admin && IsLastAdmin(account.Id))
                error = "Đây là quản trị viên cuối cùng, không thể xóa!";

            if (error != null)
            {
                TempData["Error"] = error;
                return RedirectToAction("Index");
            }

            ext.UserAccounts.Remove(account);
            ext.SaveChanges();
            TempData["Success"] = $"Đã xóa tài khoản \"{account.Username}\".";
            return RedirectToAction("Index");
        }

        private bool IsLastAdmin(int accountId)
        {
            return !ext.UserAccounts.Any(a => a.Role == UserRoles.Admin && a.Id != accountId);
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
