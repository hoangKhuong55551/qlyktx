using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using Qlykytucxa.Models;

namespace Qlykytucxa.Controllers
{
    public class RoomRegistrationsController : Controller
    {
        private MiniDormDBEntities db = new MiniDormDBEntities();

        // GET: RoomRegistrations (Tìm kiếm, Lọc theo trạng thái, Lọc sắp hết hạn)
        public ActionResult Index(string searchString, string statusFilter, string expiryFilter)
        {
            var registrations = db.RoomRegistrations.Include(r => r.Rooms).Include(r => r.Students).AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                registrations = registrations.Where(r => r.Students.FullName.Contains(searchString)
                                                      || r.Students.StudentCode.Contains(searchString)
                                                      || r.Rooms.RoomNumber.Contains(searchString));
            }

            if (!string.IsNullOrEmpty(statusFilter))
            {
                registrations = registrations.Where(r => r.Status == statusFilter);
            }

            // Lọc sắp hết hạn (trong vòng 30 ngày) hoặc đã quá hạn
            DateTime in30Days = DateTime.Now.AddDays(30);
            if (expiryFilter == "expiring")
            {
                registrations = registrations.Where(r => (r.Status == DormConstants.StatusDangO || r.Status == "Đang ở")
                                                      && r.CheckOutDate.HasValue 
                                                      && r.CheckOutDate.Value <= in30Days 
                                                      && r.CheckOutDate.Value >= DateTime.Now);
            }
            else if (expiryFilter == "expired")
            {
                registrations = registrations.Where(r => (r.Status == DormConstants.StatusDangO || r.Status == "Đang ở")
                                                      && r.CheckOutDate.HasValue 
                                                      && r.CheckOutDate.Value < DateTime.Now);
            }

            ViewBag.SearchString = searchString;
            ViewBag.StatusFilter = statusFilter;
            ViewBag.ExpiryFilter = expiryFilter;

            return View(registrations.OrderByDescending(r => r.RegistrationId).ToList());
        }

        // GET: RoomRegistrations/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            RoomRegistrations roomRegistrations = db.RoomRegistrations.Include(r => r.Rooms).Include(r => r.Students).FirstOrDefault(r => r.RegistrationId == id);
            if (roomRegistrations == null) return HttpNotFound();
            return View(roomRegistrations);
        }

        // GET: RoomRegistrations/PrintContract/5 - Mẫu In Hợp đồng nội trú chuẩn A4
        public ActionResult PrintContract(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            RoomRegistrations contract = db.RoomRegistrations.Include(r => r.Rooms).Include(r => r.Students).FirstOrDefault(r => r.RegistrationId == id);
            if (contract == null) return HttpNotFound();
            return View(contract);
        }

        // POST: RoomRegistrations/ExtendContract - Gia hạn thời hạn hợp đồng
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ExtendContract(int registrationId, DateTime newCheckOutDate)
        {
            var reg = db.RoomRegistrations.Include(r => r.Students).FirstOrDefault(r => r.RegistrationId == registrationId);
            if (reg != null)
            {
                if (newCheckOutDate <= reg.CheckInDate)
                {
                    TempData["Error"] = "Ngày gia hạn mới phải sau ngày bắt đầu vào ở!";
                    return RedirectToAction("Index");
                }

                reg.CheckOutDate = newCheckOutDate;
                db.SaveChanges();
                TempData["Success"] = $"Gia hạn hợp đồng cho sinh viên {reg.Students.FullName} đến ngày {newCheckOutDate:dd/MM/yyyy} thành công!";
            }
            return RedirectToAction("Index");
        }

        // POST: RoomRegistrations/TerminateContract - Thanh lý / Chấm dứt hợp đồng sớm
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult TerminateContract(int registrationId, string reason)
        {
            var reg = db.RoomRegistrations.Include(r => r.Students).Include(r => r.Rooms).FirstOrDefault(r => r.RegistrationId == registrationId);
            if (reg != null && (reg.Status == DormConstants.StatusDangO || reg.Status == "Đang ở"))
            {
                reg.Status = "Đã thanh lý";
                reg.CheckOutDate = DateTime.Now;

                var room = db.Rooms.Find(reg.RoomId);
                if (room != null && (room.CurrentOccupancy ?? 0) > 0)
                {
                    room.CurrentOccupancy -= 1;
                }
                db.SaveChanges();
                TempData["Success"] = $"Đã thanh lý hợp đồng của sinh viên {reg.Students.FullName} phòng {reg.Rooms.RoomNumber} thành công! (Lý do: {reason ?? "Thanh lý bình thường"})";
            }
            return RedirectToAction("Index");
        }

        // GET: RoomRegistrations/Create
        public ActionResult Create()
        {
            var availableRooms = db.Rooms.Where(r => (r.CurrentOccupancy ?? 0) < r.MaxCapacity).ToList();
            ViewBag.RoomId = new SelectList(availableRooms, "RoomId", "RoomNumber");

            var activeStudentIds = db.RoomRegistrations
                                     .Where(r => r.Status == DormConstants.StatusDangO || r.Status == "Đang ở")
                                     .Select(r => r.StudentId)
                                     .ToList();
            var freeStudents = db.Students.Where(s => !activeStudentIds.Contains(s.StudentId)).ToList();
            ViewBag.StudentId = new SelectList(freeStudents, "StudentId", "FullName");

            return View();
        }

        // POST: RoomRegistrations/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "RegistrationId,StudentId,RoomId,CheckInDate,CheckOutDate,Status")] RoomRegistrations roomRegistrations)
        {
            var room = db.Rooms.Find(roomRegistrations.RoomId);

            if (room != null && (room.CurrentOccupancy ?? 0) >= room.MaxCapacity)
            {
                ModelState.AddModelError("RoomId", $"Phòng {room.RoomNumber} đã đủ {room.MaxCapacity} người, không thể xếp thêm!");
            }

            bool alreadyHasRoom = db.RoomRegistrations.Any(r => r.StudentId == roomRegistrations.StudentId && (r.Status == DormConstants.StatusDangO || r.Status == "Đang ở"));
            if (alreadyHasRoom)
            {
                ModelState.AddModelError("StudentId", "Sinh viên này hiện đang có phòng trong KTX rồi!");
            }

            if (ModelState.IsValid)
            {
                roomRegistrations.Status = DormConstants.StatusDangO;
                if (roomRegistrations.CheckInDate == default(DateTime))
                {
                    roomRegistrations.CheckInDate = DateTime.Now;
                }

                // Nếu không nhập ngày kết thúc, mặc định hợp đồng là 6 tháng
                if (!roomRegistrations.CheckOutDate.HasValue)
                {
                    roomRegistrations.CheckOutDate = roomRegistrations.CheckInDate.AddMonths(6);
                }

                db.RoomRegistrations.Add(roomRegistrations);

                if (room != null)
                {
                    room.CurrentOccupancy = (room.CurrentOccupancy ?? 0) + 1;
                }

                db.SaveChanges();
                TempData["Success"] = "Xếp phòng cho sinh viên thành công!";
                return RedirectToAction("Index");
            }

            var availableRooms = db.Rooms.Where(r => (r.CurrentOccupancy ?? 0) < r.MaxCapacity).ToList();
            ViewBag.RoomId = new SelectList(availableRooms, "RoomId", "RoomNumber", roomRegistrations.RoomId);
            ViewBag.StudentId = new SelectList(db.Students, "StudentId", "FullName", roomRegistrations.StudentId);
            return View(roomRegistrations);
        }

        // GET: RoomRegistrations/Transfer
        public ActionResult Transfer()
        {
            var activeRegistrations = db.RoomRegistrations
                                        .Include(r => r.Students)
                                        .Include(r => r.Rooms)
                                        .Where(r => r.Status == DormConstants.StatusDangO || r.Status == "Đang ở")
                                        .ToList();

            var availableRooms = db.Rooms
                                   .Where(r => (r.CurrentOccupancy ?? 0) < r.MaxCapacity)
                                   .ToList();

            ViewBag.ActiveRegistrations = activeRegistrations;
            ViewBag.AvailableRooms = availableRooms;

            return View();
        }

        // POST: RoomRegistrations/Transfer
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Transfer(int registrationId, int newRoomId)
        {
            var oldReg = db.RoomRegistrations
                           .Include(r => r.Students)
                           .Include(r => r.Rooms)
                           .FirstOrDefault(r => r.RegistrationId == registrationId);

            if (oldReg == null || (oldReg.Status != DormConstants.StatusDangO && oldReg.Status != "Đang ở"))
            {
                TempData["Error"] = "Không tìm thấy hợp đồng nội trú hợp lệ!";
                return RedirectToAction("Transfer");
            }

            if (oldReg.RoomId == newRoomId)
            {
                TempData["Error"] = "Phòng chuyển đến không được trùng với phòng hiện tại!";
                return RedirectToAction("Transfer");
            }

            var newRoom = db.Rooms.Find(newRoomId);
            if (newRoom == null || (newRoom.CurrentOccupancy ?? 0) >= newRoom.MaxCapacity)
            {
                TempData["Error"] = "Phòng mới đã đầy hoặc không tồn tại!";
                return RedirectToAction("Transfer");
            }

            // 1. Kết thúc phòng cũ
            oldReg.Status = DormConstants.StatusDaTraPhong;
            oldReg.CheckOutDate = DateTime.Now;

            var oldRoom = db.Rooms.Find(oldReg.RoomId);
            if (oldRoom != null && (oldRoom.CurrentOccupancy ?? 0) > 0)
            {
                oldRoom.CurrentOccupancy -= 1;
            }

            // 2. Tạo phòng mới
            var newReg = new RoomRegistrations
            {
                StudentId = oldReg.StudentId,
                RoomId = newRoomId,
                CheckInDate = DateTime.Now,
                CheckOutDate = DateTime.Now.AddMonths(6),
                Status = DormConstants.StatusDangO
            };
            db.RoomRegistrations.Add(newReg);

            // 3. Tăng sĩ số phòng mới
            newRoom.CurrentOccupancy = (newRoom.CurrentOccupancy ?? 0) + 1;

            db.SaveChanges();

            TempData["Success"] = $"Chuyển sinh viên {oldReg.Students.FullName} từ phòng {oldReg.Rooms.RoomNumber} sang phòng {newRoom.RoomNumber} thành công!";
            return RedirectToAction("Index");
        }

        // POST: RoomRegistrations/CheckOut
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CheckOut(int id)
        {
            var reg = db.RoomRegistrations.Include(r => r.Students).Include(r => r.Rooms).FirstOrDefault(r => r.RegistrationId == id);
            if (reg != null && (reg.Status == DormConstants.StatusDangO || reg.Status == "Đang ở"))
            {
                reg.Status = DormConstants.StatusDaTraPhong;
                reg.CheckOutDate = DateTime.Now;

                var room = db.Rooms.Find(reg.RoomId);
                if (room != null && (room.CurrentOccupancy ?? 0) > 0)
                {
                    room.CurrentOccupancy -= 1;
                }
                db.SaveChanges();
                TempData["Success"] = $"Sinh viên {reg.Students.FullName} đã trả phòng {reg.Rooms.RoomNumber} thành công!";
            }
            return RedirectToAction("Index");
        }

        // GET: RoomRegistrations/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            RoomRegistrations roomRegistrations = db.RoomRegistrations.Find(id);
            if (roomRegistrations == null) return HttpNotFound();

            ViewBag.RoomId = new SelectList(db.Rooms, "RoomId", "RoomNumber", roomRegistrations.RoomId);
            ViewBag.StudentId = new SelectList(db.Students, "StudentId", "FullName", roomRegistrations.StudentId);
            return View(roomRegistrations);
        }

        // POST: RoomRegistrations/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "RegistrationId,StudentId,RoomId,CheckInDate,CheckOutDate,Status")] RoomRegistrations roomRegistrations)
        {
            if (ModelState.IsValid)
            {
                var oldReg = db.RoomRegistrations.AsNoTracking().FirstOrDefault(r => r.RegistrationId == roomRegistrations.RegistrationId);
                if (oldReg != null && oldReg.RoomId != roomRegistrations.RoomId)
                {
                    if (oldReg.Status == DormConstants.StatusDangO || oldReg.Status == "Đang ở")
                    {
                        var oldRoom = db.Rooms.Find(oldReg.RoomId);
                        if (oldRoom != null && (oldRoom.CurrentOccupancy ?? 0) > 0)
                            oldRoom.CurrentOccupancy -= 1;
                    }
                    if (roomRegistrations.Status == DormConstants.StatusDangO || roomRegistrations.Status == "Đang ở")
                    {
                        var newRoom = db.Rooms.Find(roomRegistrations.RoomId);
                        if (newRoom != null)
                            newRoom.CurrentOccupancy = (newRoom.CurrentOccupancy ?? 0) + 1;
                    }
                }

                db.Entry(roomRegistrations).State = EntityState.Modified;
                db.SaveChanges();
                TempData["Success"] = "Cập nhật thông tin xếp phòng thành công!";
                return RedirectToAction("Index");
            }
            ViewBag.RoomId = new SelectList(db.Rooms, "RoomId", "RoomNumber", roomRegistrations.RoomId);
            ViewBag.StudentId = new SelectList(db.Students, "StudentId", "FullName", roomRegistrations.StudentId);
            return View(roomRegistrations);
        }

        // GET: RoomRegistrations/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            RoomRegistrations roomRegistrations = db.RoomRegistrations.Find(id);
            if (roomRegistrations == null) return HttpNotFound();
            return View(roomRegistrations);
        }

        // POST: RoomRegistrations/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            RoomRegistrations roomRegistrations = db.RoomRegistrations.Find(id);

            if (roomRegistrations.Status == DormConstants.StatusDangO || roomRegistrations.Status == "Đang ở")
            {
                var room = db.Rooms.Find(roomRegistrations.RoomId);
                if (room != null && (room.CurrentOccupancy ?? 0) > 0)
                    room.CurrentOccupancy -= 1;
            }

            db.RoomRegistrations.Remove(roomRegistrations);
            db.SaveChanges();
            TempData["Success"] = "Xóa hợp đồng nội trú thành công!";
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
