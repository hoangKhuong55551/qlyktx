using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Mvc;
using Qlykytucxa.Models;

namespace Qlykytucxa.Controllers
{
    public class UtilityBillsController : Controller
    {
        private MiniDormDBEntities db = new MiniDormDBEntities();

        // GET: UtilityBills (Tìm kiếm, Lọc tháng/năm, Lọc trạng thái thanh toán)
        public ActionResult Index(string searchString, int? monthFilter, int? yearFilter, string paymentStatus)
        {
            var utilityBills = db.UtilityBills.Include(u => u.Rooms).AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                utilityBills = utilityBills.Where(u => u.Rooms.RoomNumber.Contains(searchString));
            }

            if (monthFilter.HasValue && monthFilter.Value > 0)
            {
                utilityBills = utilityBills.Where(u => u.BillingMonth == monthFilter.Value);
            }

            if (yearFilter.HasValue && yearFilter.Value > 0)
            {
                utilityBills = utilityBills.Where(u => u.BillingYear == yearFilter.Value);
            }

            if (paymentStatus == "paid")
            {
                utilityBills = utilityBills.Where(u => u.IsPaid);
            }
            else if (paymentStatus == "unpaid")
            {
                utilityBills = utilityBills.Where(u => !u.IsPaid);
            }

            ViewBag.SearchString = searchString;
            ViewBag.MonthFilter = monthFilter;
            ViewBag.YearFilter = yearFilter;
            ViewBag.PaymentStatus = paymentStatus;

            return View(utilityBills.OrderByDescending(u => u.BillingYear).ThenByDescending(u => u.BillingMonth).ToList());
        }

        // GET: UtilityBills/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            UtilityBills utilityBills = db.UtilityBills.Include(u => u.Rooms).FirstOrDefault(u => u.BillId == id);
            if (utilityBills == null) return HttpNotFound();
            return View(utilityBills);
        }

        // GET: UtilityBills/PrintInvoice/5 (Mẫu phiếu thu / Hóa đơn điện nước & VietQR)
        public ActionResult PrintInvoice(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            UtilityBills bill = db.UtilityBills.Include(u => u.Rooms).FirstOrDefault(u => u.BillId == id);
            if (bill == null) return HttpNotFound();

            // Lấy danh sách sinh viên đang ở trong phòng này để hiển thị trên hóa đơn
            var occupants = db.RoomRegistrations.Include(r => r.Students)
                .Where(r => r.RoomId == bill.RoomId && (r.Status == "Đang ở" || r.Status == DormConstants.StatusDangO))
                .Select(r => r.Students.FullName)
                .ToList();
            ViewBag.Occupants = occupants;

            return View(bill);
        }

        // GET: UtilityBills/ExportToExcel (Xuất file CSV tương thích Excel hỗ trợ tiếng Việt)
        public ActionResult ExportToExcel(string searchString, int? monthFilter, int? yearFilter, string paymentStatus)
        {
            var utilityBills = db.UtilityBills.Include(u => u.Rooms).AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                utilityBills = utilityBills.Where(u => u.Rooms.RoomNumber.Contains(searchString));
            }
            if (monthFilter.HasValue && monthFilter.Value > 0)
            {
                utilityBills = utilityBills.Where(u => u.BillingMonth == monthFilter.Value);
            }
            if (yearFilter.HasValue && yearFilter.Value > 0)
            {
                utilityBills = utilityBills.Where(u => u.BillingYear == yearFilter.Value);
            }
            if (paymentStatus == "paid")
            {
                utilityBills = utilityBills.Where(u => u.IsPaid);
            }
            else if (paymentStatus == "unpaid")
            {
                utilityBills = utilityBills.Where(u => !u.IsPaid);
            }

            var list = utilityBills.OrderByDescending(u => u.BillingYear).ThenByDescending(u => u.BillingMonth).ToList();

            var sb = new StringBuilder();
            // UTF-8 BOM để Excel hiển thị đúng tiếng Việt có dấu
            sb.Append('\uFEFF');
            sb.AppendLine("Mã HĐ,Phòng,Tòa nhà,Kỳ hóa đơn,Điện cũ,Điện mới,Tiêu thụ Điện (kWh),Tiền Điện (VNĐ),Nước cũ,Nước mới,Tiêu thụ Nước (m3),Tiền Nước (VNĐ),Tổng tiền (VNĐ),Trạng thái");

            foreach (var b in list)
            {
                int eUsed = b.NewElectricIndex - b.OldElectricIndex;
                int wUsed = b.NewWaterIndex - b.OldWaterIndex;
                decimal eCost = eUsed * DormConstants.ElectricPrice;
                decimal wCost = wUsed * DormConstants.WaterPrice;
                string roomNo = b.Rooms != null ? b.Rooms.RoomNumber : "N/A";
                string bld = (b.Rooms != null && !string.IsNullOrEmpty(b.Rooms.RoomNumber) && char.IsLetter(b.Rooms.RoomNumber[0])) ? ("Tòa " + b.Rooms.RoomNumber[0]) : "Khu KTX";
                string status = b.IsPaid ? "Đã thanh toán" : "Chưa thanh toán";

                sb.AppendLine($"HD{b.BillId:D4},{roomNo},{bld},Tháng {b.BillingMonth}/{b.BillingYear},{b.OldElectricIndex},{b.NewElectricIndex},{eUsed},{eCost},{b.OldWaterIndex},{b.NewWaterIndex},{wUsed},{wCost},{b.TotalAmount},{status}");
            }

            byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
            string fileName = $"BaoCao_HoaDonDienNuoc_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            return File(bytes, "text/csv; charset=utf-8", fileName);
        }

        // GET: UtilityBills/Create
        public ActionResult Create()
        {
            ViewBag.RoomId = new SelectList(db.Rooms.OrderBy(r => r.RoomNumber), "RoomId", "RoomNumber");
            ViewBag.ElectricPrice = DormConstants.ElectricPrice;
            ViewBag.WaterPrice = DormConstants.WaterPrice;
            return View();
        }

        // POST: UtilityBills/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "BillId,RoomId,BillingMonth,BillingYear,OldElectricIndex,NewElectricIndex,OldWaterIndex,NewWaterIndex,TotalAmount,IsPaid")] UtilityBills bill)
        {
            if (bill.NewElectricIndex < bill.OldElectricIndex)
            {
                ModelState.AddModelError("NewElectricIndex", "Chỉ số điện mới không được nhỏ hơn chỉ số cũ!");
            }
            if (bill.NewWaterIndex < bill.OldWaterIndex)
            {
                ModelState.AddModelError("NewWaterIndex", "Chỉ số nước mới không được nhỏ hơn chỉ số cũ!");
            }

            bool duplicateBill = db.UtilityBills.Any(b => b.RoomId == bill.RoomId
                                                        && b.BillingMonth == bill.BillingMonth
                                                        && b.BillingYear == bill.BillingYear);
            if (duplicateBill)
            {
                ModelState.AddModelError("BillingMonth", $"Phòng này đã có hóa đơn tháng {bill.BillingMonth}/{bill.BillingYear} rồi!");
            }

            if (ModelState.IsValid)
            {
                int electricUsed = bill.NewElectricIndex - bill.OldElectricIndex;
                int waterUsed = bill.NewWaterIndex - bill.OldWaterIndex;

                bill.TotalAmount = (electricUsed * DormConstants.ElectricPrice) + (waterUsed * DormConstants.WaterPrice);
                bill.IsPaid = false;

                db.UtilityBills.Add(bill);
                db.SaveChanges();
                TempData["Success"] = $"Lập hóa đơn điện nước tháng {bill.BillingMonth}/{bill.BillingYear} thành công!";
                return RedirectToAction("Index");
            }

            ViewBag.RoomId = new SelectList(db.Rooms.OrderBy(r => r.RoomNumber), "RoomId", "RoomNumber", bill.RoomId);
            ViewBag.ElectricPrice = DormConstants.ElectricPrice;
            ViewBag.WaterPrice = DormConstants.WaterPrice;
            return View(bill);
        }

        // POST: UtilityBills/PayBill
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult PayBill(int id, string paymentMethod = "Tiền mặt")
        {
            var bill = db.UtilityBills.Include(b => b.Rooms).FirstOrDefault(b => b.BillId == id);
            if (bill != null && !bill.IsPaid)
            {
                bill.IsPaid = true;
                db.SaveChanges();
                TempData["Success"] = $"Đã xác nhận thanh toán {bill.TotalAmount:N0} đ (Phương thức: {paymentMethod}) cho phòng {bill.Rooms.RoomNumber} thành công!";
            }
            return RedirectToAction("Index");
        }

        // GET: UtilityBills/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            UtilityBills utilityBills = db.UtilityBills.Find(id);
            if (utilityBills == null) return HttpNotFound();

            ViewBag.RoomId = new SelectList(db.Rooms.OrderBy(r => r.RoomNumber), "RoomId", "RoomNumber", utilityBills.RoomId);
            ViewBag.ElectricPrice = DormConstants.ElectricPrice;
            ViewBag.WaterPrice = DormConstants.WaterPrice;
            return View(utilityBills);
        }

        // POST: UtilityBills/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "BillId,RoomId,BillingMonth,BillingYear,OldElectricIndex,NewElectricIndex,OldWaterIndex,NewWaterIndex,TotalAmount,IsPaid")] UtilityBills utilityBills)
        {
            if (ModelState.IsValid)
            {
                int electricUsed = utilityBills.NewElectricIndex - utilityBills.OldElectricIndex;
                int waterUsed = utilityBills.NewWaterIndex - utilityBills.OldWaterIndex;

                utilityBills.TotalAmount = (electricUsed * DormConstants.ElectricPrice) + (waterUsed * DormConstants.WaterPrice);

                db.Entry(utilityBills).State = EntityState.Modified;
                db.SaveChanges();
                TempData["Success"] = "Cập nhật hóa đơn điện nước thành công!";
                return RedirectToAction("Index");
            }
            ViewBag.RoomId = new SelectList(db.Rooms.OrderBy(r => r.RoomNumber), "RoomId", "RoomNumber", utilityBills.RoomId);
            ViewBag.ElectricPrice = DormConstants.ElectricPrice;
            ViewBag.WaterPrice = DormConstants.WaterPrice;
            return View(utilityBills);
        }

        // GET: UtilityBills/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            UtilityBills utilityBills = db.UtilityBills.Find(id);
            if (utilityBills == null) return HttpNotFound();
            return View(utilityBills);
        }

        // POST: UtilityBills/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            UtilityBills utilityBills = db.UtilityBills.Find(id);
            db.UtilityBills.Remove(utilityBills);
            db.SaveChanges();
            TempData["Success"] = "Xóa hóa đơn điện nước thành công!";
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
