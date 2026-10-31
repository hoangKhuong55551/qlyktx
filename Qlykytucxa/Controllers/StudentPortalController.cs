using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using Qlykytucxa.Filters;
using Qlykytucxa.Models;

namespace Qlykytucxa.Controllers
{
    [RoleAuthorize(UserRoles.Student, UserRoles.Admin)]
    public class StudentPortalController : Controller
    {
        private MiniDormDBEntities db = new MiniDormDBEntities();
        private DormExtContext ext = new DormExtContext();

        // Sinh vien: luon la chinh minh. Admin: xem thu cong SV cua sinh vien bat ky (mac dinh SV dau tien).
        private Students GetCurrentStudent(int? previewStudentId = null)
        {
            var sessionUser = UserSession.FromSession(Session);
            if (sessionUser.Role == UserRoles.Student)
            {
                return sessionUser.StudentId.HasValue ? db.Students.Find(sessionUser.StudentId.Value) : null;
            }
            return previewStudentId.HasValue
                ? db.Students.Find(previewStudentId.Value)
                : db.Students.OrderBy(s => s.StudentCode).FirstOrDefault();
        }

        // GET: /StudentPortal/Index
        public ActionResult Index(int? studentId)
        {
            var student = GetCurrentStudent(studentId);
            if (student == null)
            {
                if (UserSession.FromSession(Session).Role == UserRoles.Student)
                {
                    // Ho so SV da bi xoa -> buoc dang nhap lai
                    Session.Clear();
                    TempData["Error"] = "Không tìm thấy hồ sơ sinh viên của bạn!";
                    return RedirectToAction("Login", "Account");
                }
                TempData["Error"] = "Chưa có sinh viên nào trong hệ thống!";
                return RedirectToAction("Index", "Home");
            }

            ViewBag.Student = student;

            // 1. Hợp đồng đang ở
            var activeReg = GetActiveRegistration(student.StudentId);
            ViewBag.ActiveRegistration = activeReg;

            // 2. Bạn cùng phòng
            var roommates = new List<Students>();
            if (activeReg != null)
            {
                roommates = db.RoomRegistrations.Include(r => r.Students)
                    .Where(r => r.RoomId == activeReg.RoomId 
                             && r.StudentId != student.StudentId 
                             && r.Status == DormConstants.StatusDangO)
                    .Select(r => r.Students)
                    .ToList();
            }
            ViewBag.Roommates = roommates;

            // 3. Hóa đơn điện nước của phòng
            var bills = new List<UtilityBills>();
            if (activeReg != null)
            {
                bills = db.UtilityBills.Where(b => b.RoomId == activeReg.RoomId)
                    .OrderByDescending(b => b.BillingYear).ThenByDescending(b => b.BillingMonth)
                    .ToList();
            }
            ViewBag.Bills = bills;

            // 4. Sự cố phòng đã gửi
            var incidents = new List<IncidentRequest>();
            if (activeReg != null)
            {
                incidents = ext.IncidentRequests.Where(i => i.RoomId == activeReg.RoomId)
                    .OrderByDescending(i => i.CreatedAt)
                    .ToList();
            }
            ViewBag.Incidents = incidents;

            // 5. Bảng tin thông báo
            ViewBag.Announcements = ext.Announcements.Where(a => a.IsActive)
                .OrderByDescending(a => a.CreatedAt)
                .Take(4)
                .ToList();

            return View();
        }

        private RoomRegistrations GetActiveRegistration(int studentId)
        {
            return db.RoomRegistrations.Include(r => r.Rooms)
                .FirstOrDefault(r => r.StudentId == studentId && r.Status == DormConstants.StatusDangO);
        }

        // GET: /StudentPortal/ReportIncident
        [RoleAuthorize(UserRoles.Student)]
        public ActionResult ReportIncident()
        {
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");

            var activeReg = GetActiveRegistration(student.StudentId);

            if (activeReg == null)
            {
                TempData["Error"] = "Bạn chưa được xếp vào phòng nào nên không thể gửi báo hỏng!";
                return RedirectToAction("Index");
            }

            var model = new IncidentRequest
            {
                RoomId = activeReg.RoomId,
                RoomNumber = activeReg.Rooms.RoomNumber,
                ReporterName = student.FullName,
                CreatedAt = DateTime.Now,
                Status = DormConstants.IncidentMoiTiepNhan
            };
            return View(model);
        }

        // POST: /StudentPortal/ReportIncident
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RoleAuthorize(UserRoles.Student)]
        public ActionResult ReportIncident(IncidentRequest model)
        {
            // Phong va nguoi bao lay tu phia server, khong tin gia tri gui len tu form
            var student = GetCurrentStudent();
            if (student == null) return RedirectToAction("Login", "Account");
            var activeReg = GetActiveRegistration(student.StudentId);
            if (activeReg == null)
            {
                TempData["Error"] = "Bạn chưa được xếp vào phòng nào nên không thể gửi báo hỏng!";
                return RedirectToAction("Index");
            }
            model.RoomId = activeReg.RoomId;
            model.RoomNumber = activeReg.Rooms.RoomNumber;
            model.ReporterName = student.FullName;
            model.AssignedTo = DormConstants.ChuaPhanCong;
            model.Cost = 0;
            model.ResolvedAt = null;
            ModelState.Remove("RoomId");
            ModelState.Remove("ReporterName");

            if (ModelState.IsValid)
            {
                model.CreatedAt = DateTime.Now;
                model.Status = DormConstants.IncidentMoiTiepNhan;
                ext.IncidentRequests.Add(model);
                ext.SaveChanges();

                TempData["Success"] = "Gửi yêu cầu báo hỏng thiết bị thành công! Ban Quản lý và thợ sửa chữa sẽ tiếp nhận xử lý sớm nhất.";
                return RedirectToAction("Index");
            }
            return View(model);
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
