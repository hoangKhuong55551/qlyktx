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
    public class RoomRegistrationsController : Controller
    {
        private static readonly string[] ValidStatuses = { DormConstants.StatusDangO, DormConstants.StatusDaTraPhong, DormConstants.StatusDaThanhLy };

        private MiniDormDBEntities db = new MiniDormDBEntities();
        private DormExtContext ext = new DormExtContext();

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
                registrations = registrations.Where(r => r.Status == DormConstants.StatusDangO
                                                      && r.CheckOutDate.HasValue
                                                      && r.CheckOutDate.Value <= in30Days
                                                      && r.CheckOutDate.Value >= DateTime.Now);
            }
            else if (expiryFilter == "expired")
            {
                registrations = registrations.Where(r => r.Status == DormConstants.StatusDangO
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
        // Sinh vien chi duoc in hop dong cua chinh minh
        [RoleAuthorize(UserRoles.Admin, UserRoles.Student)]
        public ActionResult PrintContract(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            RoomRegistrations contract = db.RoomRegistrations.Include(r => r.Rooms).Include(r => r.Students).FirstOrDefault(r => r.RegistrationId == id);
            if (contract == null) return HttpNotFound();

            var user = UserSession.FromSession(Session);
            if (user.Role == UserRoles.Student && contract.StudentId != user.StudentId)
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
            }
            return View(contract);
        }

        // POST: RoomRegistrations/ExtendContract - Gia hạn thời hạn hợp đồng
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ExtendContract(int registrationId, DateTime newCheckOutDate)
        {
            var reg = db.RoomRegistrations.Include(r => r.Students).FirstOrDefault(r => r.RegistrationId == registrationId);
            if (reg != null && reg.Status == DormConstants.StatusDangO)
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
            if (reg != null && reg.Status == DormConstants.StatusDangO)
            {
                RoomFeeService.EnsureMonthlyFees(db, ext); // chot tien phong den thang hien tai truoc khi ket thuc
                reg.Status = DormConstants.StatusDaThanhLy;
                reg.CheckOutDate = DateTime.Now;
                db.SaveChanges();
                OccupancyService.Recalculate(db);
                TempData["Success"] = $"Đã thanh lý hợp đồng của sinh viên {reg.Students.FullName} phòng {reg.Rooms.RoomNumber} thành công! (Lý do: {reason ?? "Thanh lý bình thường"})";
            }
            return RedirectToAction("Index");
        }

        private void PopulateCreateLists(int? roomId = null, int? studentId = null)
        {
            var availableRooms = db.Rooms.Where(r => (r.CurrentOccupancy ?? 0) < r.MaxCapacity).OrderBy(r => r.RoomNumber).ToList();
            ViewBag.RoomId = new SelectList(availableRooms, "RoomId", "RoomNumber", roomId);

            var freeStudents = db.Students
                .Where(s => !s.RoomRegistrations.Any(r => r.Status == DormConstants.StatusDangO))
                .OrderBy(s => s.StudentCode)
                .ToList();
            ViewBag.StudentId = new SelectList(freeStudents, "StudentId", "FullName", studentId);
        }

        // GET: RoomRegistrations/Create
        public ActionResult Create()
        {
            PopulateCreateLists();
            return View();
        }

        // POST: RoomRegistrations/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "StudentId,RoomId,CheckInDate,CheckOutDate")] RoomRegistrations roomRegistrations)
        {
            var room = db.Rooms.Find(roomRegistrations.RoomId);
            if (room == null)
            {
                ModelState.AddModelError("RoomId", "Vui lòng chọn phòng hợp lệ!");
            }
            else if (OccupancyService.ActiveCount(db, room.RoomId) >= room.MaxCapacity)
            {
                ModelState.AddModelError("RoomId", $"Phòng {room.RoomNumber} đã đủ {room.MaxCapacity} người, không thể xếp thêm!");
            }

            bool alreadyHasRoom = db.RoomRegistrations.Any(r => r.StudentId == roomRegistrations.StudentId && r.Status == DormConstants.StatusDangO);
            if (alreadyHasRoom)
            {
                ModelState.AddModelError("StudentId", "Sinh viên này hiện đang có phòng trong KTX rồi!");
            }

            if (roomRegistrations.CheckInDate == default(DateTime))
            {
                roomRegistrations.CheckInDate = DateTime.Today;
            }
            // Nếu không nhập ngày kết thúc, mặc định hợp đồng là 6 tháng
            if (!roomRegistrations.CheckOutDate.HasValue)
            {
                roomRegistrations.CheckOutDate = roomRegistrations.CheckInDate.AddMonths(6);
            }
            if (roomRegistrations.CheckOutDate.Value <= roomRegistrations.CheckInDate)
            {
                ModelState.AddModelError("CheckOutDate", "Ngày trả phòng phải sau ngày vào ở!");
            }

            if (ModelState.IsValid)
            {
                roomRegistrations.Status = DormConstants.StatusDangO;
                db.RoomRegistrations.Add(roomRegistrations);
                db.SaveChanges();
                OccupancyService.Recalculate(db);

                TempData["Success"] = "Xếp phòng cho sinh viên thành công!";
                return RedirectToAction("Index");
            }

            PopulateCreateLists(roomRegistrations.RoomId, roomRegistrations.StudentId);
            return View(roomRegistrations);
        }

        // GET: RoomRegistrations/Transfer
        public ActionResult Transfer()
        {
            var activeRegistrations = db.RoomRegistrations
                                        .Include(r => r.Students)
                                        .Include(r => r.Rooms)
                                        .Where(r => r.Status == DormConstants.StatusDangO)
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

            if (oldReg == null || oldReg.Status != DormConstants.StatusDangO)
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
            if (newRoom == null || OccupancyService.ActiveCount(db, newRoomId) >= newRoom.MaxCapacity)
            {
                TempData["Error"] = "Phòng mới đã đầy hoặc không tồn tại!";
                return RedirectToAction("Transfer");
            }

            RoomFeeService.EnsureMonthlyFees(db, ext); // chot tien phong den thang hien tai truoc khi ket thuc

            // 1. Kết thúc hợp đồng phòng cũ
            oldReg.Status = DormConstants.StatusDaTraPhong;
            oldReg.CheckOutDate = DateTime.Now;

            // 2. Tạo hợp đồng phòng mới (cùng 1 lần SaveChanges -> cùng transaction)
            db.RoomRegistrations.Add(new RoomRegistrations
            {
                StudentId = oldReg.StudentId,
                RoomId = newRoomId,
                CheckInDate = DateTime.Today,
                CheckOutDate = DateTime.Today.AddMonths(6),
                Status = DormConstants.StatusDangO
            });
            db.SaveChanges();
            OccupancyService.Recalculate(db);

            TempData["Success"] = $"Chuyển sinh viên {oldReg.Students.FullName} từ phòng {oldReg.Rooms.RoomNumber} sang phòng {newRoom.RoomNumber} thành công!";
            return RedirectToAction("Index");
        }

        // POST: RoomRegistrations/CheckOut
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CheckOut(int id)
        {
            var reg = db.RoomRegistrations.Include(r => r.Students).Include(r => r.Rooms).FirstOrDefault(r => r.RegistrationId == id);
            if (reg != null && reg.Status == DormConstants.StatusDangO)
            {
                RoomFeeService.EnsureMonthlyFees(db, ext); // chot tien phong den thang hien tai truoc khi ket thuc
                reg.Status = DormConstants.StatusDaTraPhong;
                reg.CheckOutDate = DateTime.Now;
                db.SaveChanges();
                OccupancyService.Recalculate(db);
                TempData["Success"] = $"Sinh viên {reg.Students.FullName} đã trả phòng {reg.Rooms.RoomNumber} thành công!";
            }
            return RedirectToAction("Index");
        }

        private void PopulateEditLists(RoomRegistrations reg)
        {
            ViewBag.RoomId = new SelectList(db.Rooms.OrderBy(r => r.RoomNumber), "RoomId", "RoomNumber", reg.RoomId);
            ViewBag.StudentId = new SelectList(db.Students.OrderBy(s => s.StudentCode), "StudentId", "FullName", reg.StudentId);
            ViewBag.StatusList = new SelectList(ValidStatuses, reg.Status);
        }

        // GET: RoomRegistrations/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            RoomRegistrations roomRegistrations = db.RoomRegistrations.Find(id);
            if (roomRegistrations == null) return HttpNotFound();

            PopulateEditLists(roomRegistrations);
            return View(roomRegistrations);
        }

        // POST: RoomRegistrations/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "RegistrationId,StudentId,RoomId,CheckInDate,CheckOutDate,Status")] RoomRegistrations roomRegistrations)
        {
            var existing = db.RoomRegistrations.Find(roomRegistrations.RegistrationId);
            if (existing == null) return HttpNotFound();
            string oldStatus = existing.Status;

            if (!ValidStatuses.Contains(roomRegistrations.Status))
            {
                ModelState.AddModelError("Status", "Trạng thái không hợp lệ!");
            }
            if (roomRegistrations.CheckOutDate.HasValue && roomRegistrations.CheckOutDate.Value <= roomRegistrations.CheckInDate)
            {
                ModelState.AddModelError("CheckOutDate", "Ngày trả phòng phải sau ngày vào ở!");
            }

            if (roomRegistrations.Status == DormConstants.StatusDangO)
            {
                var room = db.Rooms.Find(roomRegistrations.RoomId);
                if (room == null)
                {
                    ModelState.AddModelError("RoomId", "Vui lòng chọn phòng hợp lệ!");
                }
                else if (OccupancyService.ActiveCount(db, room.RoomId, roomRegistrations.RegistrationId) >= room.MaxCapacity)
                {
                    ModelState.AddModelError("RoomId", $"Phòng {room.RoomNumber} đã đủ {room.MaxCapacity} người!");
                }

                bool hasOtherActive = db.RoomRegistrations.Any(r => r.StudentId == roomRegistrations.StudentId
                                                                 && r.RegistrationId != roomRegistrations.RegistrationId
                                                                 && r.Status == DormConstants.StatusDangO);
                if (hasOtherActive)
                {
                    ModelState.AddModelError("StudentId", "Sinh viên này đang có một hợp đồng khác còn hiệu lực!");
                }
            }

            if (ModelState.IsValid)
            {
                if (oldStatus == DormConstants.StatusDangO && roomRegistrations.Status != DormConstants.StatusDangO)
                {
                    RoomFeeService.EnsureMonthlyFees(db, ext); // chot tien phong truoc khi ket thuc hop dong
                }

                existing.StudentId = roomRegistrations.StudentId;
                existing.RoomId = roomRegistrations.RoomId;
                existing.CheckInDate = roomRegistrations.CheckInDate;
                existing.CheckOutDate = roomRegistrations.CheckOutDate;
                existing.Status = roomRegistrations.Status;
                db.SaveChanges();
                OccupancyService.Recalculate(db);
                TempData["Success"] = "Cập nhật thông tin xếp phòng thành công!";
                return RedirectToAction("Index");
            }
            PopulateEditLists(roomRegistrations);
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
            if (roomRegistrations == null) return HttpNotFound();

            // Xoa cac khoan tien phong chua thu va hop dong trong cung 1 transaction
            using (var tx = db.Database.BeginTransaction())
            using (var shared = DormExtContext.ShareConnection(db))
            {
                shared.Database.UseTransaction(tx.UnderlyingTransaction);

                var fees = shared.RoomFeePayments.Where(f => f.RegistrationId == id).ToList();
                var feeIds = fees.Select(f => f.Id).ToList();
                if (fees.Any(f => f.IsPaid)
                    || shared.CashFlowTransactions.Any(t => t.RoomFeePaymentId != null && feeIds.Contains(t.RoomFeePaymentId.Value)))
                {
                    TempData["Error"] = "Không thể xóa hợp đồng đã có phiếu thu tiền phòng. Hãy dùng chức năng Trả phòng / Thanh lý.";
                    return RedirectToAction("Index");
                }
                if (shared.Deposits.Any(d => d.RegistrationId == id))
                {
                    TempData["Error"] = "Không thể xóa hợp đồng đã có lịch sử đặt cọc. Hãy dùng chức năng Trả phòng / Thanh lý.";
                    return RedirectToAction("Index");
                }
                shared.RoomFeePayments.RemoveRange(fees);
                shared.SaveChanges();

                db.RoomRegistrations.Remove(roomRegistrations);
                db.SaveChanges();
                tx.Commit();
            }
            OccupancyService.Recalculate(db);

            TempData["Success"] = "Xóa hợp đồng nội trú thành công!";
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
