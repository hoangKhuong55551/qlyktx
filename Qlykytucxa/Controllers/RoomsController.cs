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

namespace Qlykytucxa.Controllers
{
    [RoleAuthorize(UserRoles.Admin)]
    public class RoomsController : Controller
    {
        private MiniDormDBEntities db = new MiniDormDBEntities();

        // GET: Rooms (Tìm kiếm, Lọc tòa nhà, Lọc tình trạng phòng & Tự động đồng bộ số người đang ở)
        public ActionResult Index(string searchString, string buildingFilter, string statusFilter)
        {
            // TỰ ĐỘNG ĐỒNG BỘ: Đếm chính xác số sinh viên thực tế đang ở để cập nhật CurrentOccupancy vào database
            var allRooms = db.Rooms.Include(r => r.RoomRegistrations).ToList();
            bool needSave = false;
            foreach (var r in allRooms)
            {
                int actualOcc = r.RoomRegistrations.Count(reg => reg.Status == DormConstants.StatusDangO 
                                                              || reg.Status == "Đang ở" 
                                                              || reg.Status == "Dang o");
                if (r.CurrentOccupancy != actualOcc)
                {
                    r.CurrentOccupancy = actualOcc;
                    needSave = true;
                }
            }
            if (needSave)
            {
                db.SaveChanges();
            }

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

            int actualOccupancy = rooms.RoomRegistrations.Count(r => r.Status == DormConstants.StatusDangO 
                                                                 || r.Status == "Đang ở" 
                                                                 || r.Status == "Dang o");
            if (rooms.CurrentOccupancy != actualOccupancy)
            {
                rooms.CurrentOccupancy = actualOccupancy;
                db.SaveChanges();
            }

            return View(rooms);
        }

        // POST: Rooms/RemoveStudent - Xóa sinh viên ra khỏi phòng
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RemoveStudent(int registrationId, int roomId)
        {
            var reg = db.RoomRegistrations.Include(r => r.Students).FirstOrDefault(r => r.RegistrationId == registrationId);
            if (reg != null && (reg.Status == DormConstants.StatusDangO || reg.Status == "Đang ở" || reg.Status == "Dang o"))
            {
                reg.Status = DormConstants.StatusDaTraPhong;
                reg.CheckOutDate = DateTime.Now;

                var room = db.Rooms.Find(roomId);
                if (room != null && (room.CurrentOccupancy ?? 0) > 0)
                {
                    room.CurrentOccupancy -= 1;
                }

                db.SaveChanges();
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
            return View();
        }

        // POST: Rooms/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "RoomId,RoomNumber,RoomType,MaxCapacity,CurrentOccupancy,RoomPrice")] Rooms rooms)
        {
            if (db.Rooms.Any(r => r.RoomNumber == rooms.RoomNumber))
            {
                ModelState.AddModelError("RoomNumber", "Số phòng này đã tồn tại trong Ký túc xá!");
            }

            if (ModelState.IsValid)
            {
                if (rooms.CurrentOccupancy == null) rooms.CurrentOccupancy = 0;
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
        public ActionResult Edit([Bind(Include = "RoomId,RoomNumber,RoomType,MaxCapacity,CurrentOccupancy,RoomPrice")] Rooms rooms)
        {
            if (db.Rooms.Any(r => r.RoomNumber == rooms.RoomNumber && r.RoomId != rooms.RoomId))
            {
                ModelState.AddModelError("RoomNumber", "Số phòng này đã trùng với một phòng khác!");
            }

            if (ModelState.IsValid)
            {
                db.Entry(rooms).State = EntityState.Modified;
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
            bool hasActiveStudents = db.RoomRegistrations.Any(r => r.RoomId == id && (r.Status == DormConstants.StatusDangO || r.Status == "Đang ở" || r.Status == "Dang o"));
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

            Rooms rooms = db.Rooms.Find(id);
            db.Rooms.Remove(rooms);
            db.SaveChanges();
            TempData["Success"] = "Xóa phòng KTX thành công!";
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
