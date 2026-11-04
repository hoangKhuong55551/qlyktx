using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using Qlykytucxa.Filters;
using Qlykytucxa.Models;
using Qlykytucxa.Services;

namespace Qlykytucxa.Controllers
{
    [RoleAuthorize(UserRoles.Admin)]
    public class StudentsController : Controller
    {
        private MiniDormDBEntities db = new MiniDormDBEntities();
        private DormExtContext ext = new DormExtContext();

        // GET: Students (Tìm kiếm, Lọc theo trạng thái nội trú, Phân trang)
        public ActionResult Index(string searchString, string dormFilter, int page = 1)
        {
            int pageSize = 10;
            var students = db.Students.Include(s => s.RoomRegistrations.Select(r => r.Rooms)).AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                students = students.Where(s => s.FullName.Contains(searchString)
                                            || s.StudentCode.Contains(searchString)
                                            || s.ClassName.Contains(searchString)
                                            || s.Phone.Contains(searchString));
            }

            if (dormFilter == "has_room")
            {
                students = students.Where(s => s.RoomRegistrations.Any(r => r.Status == DormConstants.StatusDangO));
            }
            else if (dormFilter == "no_room")
            {
                students = students.Where(s => !s.RoomRegistrations.Any(r => r.Status == DormConstants.StatusDangO));
            }

            int totalItems = students.Count();
            int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

            var pagedStudents = students.OrderBy(s => s.StudentCode)
                                        .Skip((page - 1) * pageSize)
                                        .Take(pageSize)
                                        .ToList();

            ViewBag.SearchString = searchString;
            ViewBag.DormFilter = dormFilter;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;

            return View(pagedStudents);
        }

        // GET: Students/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            Students students = db.Students.Include(s => s.RoomRegistrations.Select(r => r.Rooms)).FirstOrDefault(s => s.StudentId == id);
            if (students == null) return HttpNotFound();
            return View(students);
        }

        // GET: Students/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: Students/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "StudentId,StudentCode,FullName,Gender,Phone,ClassName")] Students students)
        {
            if (db.Students.Any(s => s.StudentCode == students.StudentCode))
            {
                ModelState.AddModelError("StudentCode", "Mã sinh viên này đã tồn tại trong hệ thống!");
            }

            if (ModelState.IsValid)
            {
                db.Students.Add(students);
                db.SaveChanges();
                AccountService.SyncAccounts(db, ext); // tao tai khoan dang nhap (ten = ma SV, mat khau mac dinh, bat doi)
                TempData["Success"] = $"Thêm mới sinh viên {students.FullName} thành công! Tài khoản đăng nhập: {students.StudentCode} / mật khẩu mặc định {AccountService.DefaultPassword} (bắt buộc đổi khi đăng nhập lần đầu).";
                return RedirectToAction("Index");
            }

            return View(students);
        }

        // GET: Students/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            Students students = db.Students.Find(id);
            if (students == null) return HttpNotFound();
            return View(students);
        }

        // POST: Students/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "StudentId,StudentCode,FullName,Gender,Phone,ClassName")] Students students)
        {
            if (db.Students.Any(s => s.StudentCode == students.StudentCode && s.StudentId != students.StudentId))
            {
                ModelState.AddModelError("StudentCode", "Mã sinh viên này đã thuộc về sinh viên khác!");
            }

            if (ModelState.IsValid)
            {
                db.Entry(students).State = EntityState.Modified;
                db.SaveChanges();
                AccountService.SyncAccounts(db, ext); // doi ma SV -> doi ten dang nhap theo
                TempData["Success"] = $"Cập nhật hồ sơ sinh viên {students.FullName} thành công!";
                return RedirectToAction("Index");
            }
            return View(students);
        }

        // POST: Students/ResetPassword/5 - Cap mat khau tam moi (SV quen mat khau / tai khoan bi khoa)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetPassword(int id)
        {
            var student = db.Students.Find(id);
            if (student == null) return HttpNotFound();

            AccountService.SyncAccounts(db, ext);
            var account = ext.UserAccounts.FirstOrDefault(a => a.StudentId == id);
            if (account == null)
            {
                TempData["Error"] = "Không tạo được tài khoản cho sinh viên này (mã SV trống hoặc trùng tên tài khoản khác)!";
                return RedirectToAction("Details", new { id });
            }

            string temp = AccountService.GenerateTemporaryPassword();
            account.PasswordHash = AccountService.HashPassword(temp);
            account.MustChangePassword = true;
            account.FailedLoginCount = 0;
            account.LockoutEnd = null;
            ext.SaveChanges();

            TempData["Success"] = $"Đã đặt lại mật khẩu cho tài khoản {account.Username}. Mật khẩu tạm: {temp} (chỉ hiển thị một lần, sinh viên phải đổi khi đăng nhập).";
            return RedirectToAction("Details", new { id });
        }

        // GET: Students/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            Students students = db.Students.Find(id);
            if (students == null) return HttpNotFound();
            return View(students);
        }

        // POST: Students/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            bool isLiving = db.RoomRegistrations.Any(r => r.StudentId == id && r.Status == DormConstants.StatusDangO);
            if (isLiving)
            {
                ModelState.AddModelError("", "Không thể xóa sinh viên đang cư trú trong KTX. Hãy làm thủ tục trả phòng trước!");
                Students studentErr = db.Students.Find(id);
                return View(studentErr);
            }

            bool hasHistory = db.RoomRegistrations.Any(r => r.StudentId == id);
            if (hasHistory)
            {
                ModelState.AddModelError("", "Không thể xóa sinh viên còn lịch sử xếp phòng. Hãy xóa các bản ghi lịch sử trước.");
                Students studentErr = db.Students.Find(id);
                return View(studentErr);
            }

            Students students = db.Students.Find(id);
            if (students == null) return HttpNotFound();
            db.Students.Remove(students);
            db.SaveChanges();
            TempData["Success"] = "Xóa hồ sơ sinh viên thành công!";
            return RedirectToAction("Index");
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
