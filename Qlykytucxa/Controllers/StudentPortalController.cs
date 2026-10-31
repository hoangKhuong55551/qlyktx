using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using Newtonsoft.Json;
using Qlykytucxa.Filters;
using Qlykytucxa.Models;

namespace Qlykytucxa.Controllers
{
    [RoleAuthorize(UserRoles.Student, UserRoles.Admin)]
    public class StudentPortalController : Controller
    {
        private MiniDormDBEntities db = new MiniDormDBEntities();

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
            var activeReg = db.RoomRegistrations.Include(r => r.Rooms)
                .FirstOrDefault(r => r.StudentId == student.StudentId && (r.Status == "Đang ở" || r.Status == DormConstants.StatusDangO));
            ViewBag.ActiveRegistration = activeReg;

            // 2. Bạn cùng phòng
            var roommates = new List<Students>();
            if (activeReg != null)
            {
                roommates = db.RoomRegistrations.Include(r => r.Students)
                    .Where(r => r.RoomId == activeReg.RoomId 
                             && r.StudentId != student.StudentId 
                             && (r.Status == "Đang ở" || r.Status == DormConstants.StatusDangO))
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
                incidents = LoadRoomIncidents(activeReg.RoomId);
            }
            ViewBag.Incidents = incidents;

            // 5. Bảng tin thông báo
            ViewBag.Announcements = LoadActiveAnnouncements();

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
                Status = "Mới tiếp nhận"
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
            model.AssignedTo = "Chưa phân công";
            model.Cost = 0;
            model.ResolvedAt = null;
            ModelState.Remove("RoomId");
            ModelState.Remove("ReporterName");

            if (ModelState.IsValid)
            {
                string appData = Server.MapPath("~/App_Data");
                string path = Path.Combine(appData, "incidents.json");
                var list = new List<IncidentRequest>();

                if (System.IO.File.Exists(path))
                {
                    string json = System.IO.File.ReadAllText(path);
                    list = JsonConvert.DeserializeObject<List<IncidentRequest>>(json) ?? new List<IncidentRequest>();
                }

                int nextId = list.Any() ? list.Max(x => x.Id) + 1 : 1;
                model.Id = nextId;
                model.CreatedAt = DateTime.Now;
                model.Status = "Mới tiếp nhận";

                list.Insert(0, model);
                System.IO.File.WriteAllText(path, JsonConvert.SerializeObject(list, Formatting.Indented));

                TempData["Success"] = "Gửi yêu cầu báo hỏng thiết bị thành công! Ban Quản lý và thợ sửa chữa sẽ tiếp nhận xử lý sớm nhất.";
                return RedirectToAction("Index");
            }
            return View(model);
        }

        private List<IncidentRequest> LoadRoomIncidents(int roomId)
        {
            try
            {
                string path = Path.Combine(Server.MapPath("~/App_Data"), "incidents.json");
                if (System.IO.File.Exists(path))
                {
                    string json = System.IO.File.ReadAllText(path);
                    var list = JsonConvert.DeserializeObject<List<IncidentRequest>>(json) ?? new List<IncidentRequest>();
                    return list.Where(i => i.RoomId == roomId).OrderByDescending(i => i.CreatedAt).ToList();
                }
            }
            catch { }
            return new List<IncidentRequest>();
        }

        private List<Announcement> LoadActiveAnnouncements()
        {
            try
            {
                string path = Path.Combine(Server.MapPath("~/App_Data"), "announcements.json");
                if (System.IO.File.Exists(path))
                {
                    string json = System.IO.File.ReadAllText(path);
                    var list = JsonConvert.DeserializeObject<List<Announcement>>(json) ?? new List<Announcement>();
                    return list.Where(a => a.IsActive).OrderByDescending(a => a.CreatedAt).Take(4).ToList();
                }
            }
            catch { }
            return new List<Announcement>();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
