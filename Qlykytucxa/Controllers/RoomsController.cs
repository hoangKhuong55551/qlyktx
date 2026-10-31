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
    public class RoomsController : Controller
    {
        private MiniDormDBEntities db = new MiniDormDBEntities();
        private DormExtContext ext = new DormExtContext();

        // GET: Rooms (Tìm kiếm, Lọc tòa nhà, Lọc tình trạng phòng)
        public ActionResult Index(string searchString, string buildingFilter, string statusFilter)
        {
            var rooms = db.Rooms.Include(r => r.RoomRegistrations).AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                rooms = rooms.Where(r => r.RoomNumber.Contains(searchString) || r.RoomType.Contains(searchString));
            }

            if (!string.IsNullOrEmpty(buildingFilter))
            {
                rooms = rooms.Where(r => r.RoomNumber.StartsWith(buildingFilter));
            }

            if (statusFilter == "available")
            {
                rooms = rooms.Where(r => (r.CurrentOccupancy ?? 0) < r.MaxCapacity);
            }
            else if (statusFilter == "full")
            {
                rooms = rooms.Where(r => (r.CurrentOccupancy ?? 0) >= r.MaxCapacity);
            }
            else if (statusFilter == "empty")
            {
                rooms = rooms.Where(r => (r.CurrentOccupancy ?? 0) == 0);
            }

            ViewBag.SearchString = searchString;
            ViewBag.BuildingFilter = buildingFilter;
            ViewBag.StatusFilter = statusFilter;

            return View(rooms.OrderBy(r => r.RoomNumber).ToList());
        }

        // GET: Rooms/Details/5 (Hiển thị chi tiết phòng và danh sách sinh viên đang ở)
        public ActionResult Details(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            
            Rooms rooms = db.Rooms
                            .Include(r => r.RoomRegistrations.Select(reg => reg.Students))
                            .FirstOrDefault(r => r.RoomId == id);
            
            if (rooms == null) return HttpNotFound();
            return View(rooms);
        }

        // POST: Rooms/RemoveStudent - Xóa sinh viên ra khỏi phòng
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RemoveStudent(int registrationId, int roomId)
        {
            var reg = db.RoomRegistrations.Include(r => r.Students).FirstOrDefault(r => r.RegistrationId == registrationId);
            if (reg != null && reg.RoomId == roomId && reg.Status == DormConstants.StatusDangO)
            {
                RoomFeeService.EnsureMonthlyFees(db, ext); // chot tien phong den thang hien tai truoc khi ket thuc
                reg.Status = DormConstants.StatusDaTraPhong;
                reg.CheckOutDate = DateTime.Now;
                db.SaveChanges();
                OccupancyService.Recalculate(db);

                var room = db.Rooms.Find(roomId);
                TempData["Success"] = $"Đã đưa sinh viên {reg.Students.FullName} ({reg.Students.StudentCode}) ra khỏi phòng {room.RoomNumber} thành công!";
            }
            else
            {
                TempData["Error"] = "Không tìm thấy thông tin cư trú hợp lệ của sinh viên!";
            }

            return RedirectToAction("Details", new { id = roomId });
        }

        // GET: Rooms/Create
        public ActionResult Create()
        {
            return View(new Rooms { CurrentOccupancy = 0 });
        }

        // POST: Rooms/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "RoomNumber,RoomType,MaxCapacity,RoomPrice")] Rooms rooms)
        {
            if (db.Rooms.Any(r => r.RoomNumber == rooms.RoomNumber))
            {
                ModelState.AddModelError("RoomNumber", "Số phòng này đã tồn tại trong Ký túc xá!");
            }

            if (ModelState.IsValid)
            {
                rooms.CurrentOccupancy = 0; // so nguoi o duoc tinh tu hop dong, khong nhap tay
                db.Rooms.Add(rooms);
                db.SaveChanges();
                TempData["Success"] = $"Thêm mới phòng {rooms.RoomNumber} thành công!";
                return RedirectToAction("Index");
            }

            return View(rooms);
        }

        // GET: Rooms/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            Rooms rooms = db.Rooms.Find(id);
            if (rooms == null) return HttpNotFound();
            return View(rooms);
        }

        // POST: Rooms/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "RoomId,RoomNumber,RoomType,MaxCapacity,RoomPrice")] Rooms rooms)
        {
            var existing = db.Rooms.Find(rooms.RoomId);
            if (existing == null) return HttpNotFound();

            if (db.Rooms.Any(r => r.RoomNumber == rooms.RoomNumber && r.RoomId != rooms.RoomId))
            {
                ModelState.AddModelError("RoomNumber", "Số phòng này đã trùng với một phòng khác!");
            }

            int activeCount = OccupancyService.ActiveCount(db, rooms.RoomId);
            if (rooms.MaxCapacity < activeCount)
            {
                ModelState.AddModelError("MaxCapacity", $"Phòng đang có {activeCount} sinh viên ở, sức chứa không được nhỏ hơn số này!");
            }

            if (ModelState.IsValid)
            {
                // So nguoi dang o (CurrentOccupancy) giu nguyen, khong cho sua tay
                existing.RoomNumber = rooms.RoomNumber;
                existing.RoomType = rooms.RoomType;
                existing.MaxCapacity = rooms.MaxCapacity;
                existing.RoomPrice = rooms.RoomPrice;
                db.SaveChanges();
                TempData["Success"] = $"Cập nhật thông tin phòng {rooms.RoomNumber} thành công!";
                return RedirectToAction("Index");
            }
            return View(rooms);
        }

        // GET: Rooms/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            Rooms rooms = db.Rooms.Find(id);
            if (rooms == null) return HttpNotFound();
            return View(rooms);
        }

        // POST: Rooms/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            if (db.Rooms.Find(id) == null) return HttpNotFound();

            bool hasActiveStudents = db.RoomRegistrations.Any(r => r.RoomId == id && r.Status == DormConstants.StatusDangO);
            if (hasActiveStudents)
            {
                ModelState.AddModelError("", "Không thể xóa phòng đang có sinh viên cư trú!");
                Rooms roomErr = db.Rooms.Find(id);
                return View(roomErr);
            }

            bool hasRegistrations = db.RoomRegistrations.Any(r => r.RoomId == id);
            if (hasRegistrations)
            {
                ModelState.AddModelError("", "Không thể xóa phòng còn lịch sử xếp phòng. Hãy xóa lịch sử trước.");
                Rooms roomErr = db.Rooms.Find(id);
                return View(roomErr);
            }

            bool hasUtilityBills = db.UtilityBills.Any(b => b.RoomId == id);
            if (hasUtilityBills)
            {
                ModelState.AddModelError("", "Không thể xóa phòng còn hóa đơn điện nước. Hãy xóa hóa đơn trước.");
                Rooms roomErr = db.Rooms.Find(id);
                return View(roomErr);
            }

            if (ext.IncidentRequests.Any(i => i.RoomId == id))
            {
                ModelState.AddModelError("", "Không thể xóa phòng còn lịch sử báo hỏng / sửa chữa. Hãy xóa các yêu cầu sự cố trước.");
                Rooms roomErr = db.Rooms.Find(id);
                return View(roomErr);
            }

            Rooms rooms = db.Rooms.Find(id);
            db.Rooms.Remove(rooms);
            db.SaveChanges();
            TempData["Success"] = "Xóa phòng KTX thành công!";
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
