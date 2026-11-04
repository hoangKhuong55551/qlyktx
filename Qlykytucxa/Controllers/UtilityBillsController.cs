using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Mvc;
using Qlykytucxa.Filters;
using Qlykytucxa.Models;
using Qlykytucxa.Services;

namespace Qlykytucxa.Controllers
{
    [RoleAuthorize(UserRoles.Admin)]
    public class UtilityBillsController : Controller
    {
        private MiniDormDBEntities db = new MiniDormDBEntities();
        private DormExtContext ext = new DormExtContext();

        // Hien don gia dang ap dung cho ky hoa don tren form Lap/Sua
        private void SetPriceViewBag(int year, int month)
        {
            var price = FinanceService.GetUtilityPrice(ext, year, month);
            ViewBag.ElectricPrice = price.ElectricPrice;
            ViewBag.WaterPrice = price.WaterPrice;
            ViewBag.PricePeriod = $"{month:D2}/{year}";
        }

        // Tinh tien theo don gia cua ky hoa don va luu lai don gia da ap dung
        private void ApplyPrice(UtilityBills bill)
        {
            var price = FinanceService.GetUtilityPrice(ext, bill.BillingYear, bill.BillingMonth);
            bill.ElectricUnitPrice = price.ElectricPrice;
            bill.WaterUnitPrice = price.WaterPrice;
            bill.TotalAmount = (bill.NewElectricIndex - bill.OldElectricIndex) * price.ElectricPrice
                             + (bill.NewWaterIndex - bill.OldWaterIndex) * price.WaterPrice;
        }

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
        // Sinh vien chi duoc xem hoa don cua phong minh da/dang o
        [RoleAuthorize(UserRoles.Admin, UserRoles.Student)]
        public ActionResult PrintInvoice(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            UtilityBills bill = db.UtilityBills.Include(u => u.Rooms).FirstOrDefault(u => u.BillId == id);
            if (bill == null) return HttpNotFound();

            var user = UserSession.FromSession(Session);
            if (user.Role == UserRoles.Student
                && !db.RoomRegistrations.Any(r => r.StudentId == user.StudentId && r.RoomId == bill.RoomId))
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
            }

            // Lấy danh sách sinh viên đang ở trong phòng này để hiển thị trên hóa đơn
            var occupants = db.RoomRegistrations.Include(r => r.Students)
                .Where(r => r.RoomId == bill.RoomId && r.Status == DormConstants.StatusDangO)
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
                decimal eCost = eUsed * (b.ElectricUnitPrice ?? DormConstants.ElectricPrice);
                decimal wCost = wUsed * (b.WaterUnitPrice ?? DormConstants.WaterPrice);
                string roomNo = b.Rooms != null ? b.Rooms.RoomNumber : "N/A";
                string bld = (b.Rooms != null && !string.IsNullOrEmpty(b.Rooms.RoomNumber) && char.IsLetter(b.Rooms.RoomNumber[0])) ? ("Tòa " + b.Rooms.RoomNumber[0]) : "Khu KTX";
                string status = b.IsPaid ? "Đã thanh toán" : "Chưa thanh toán";

                sb.AppendLine(CsvHelper.Row($"HD{b.BillId:D4}", roomNo, bld, $"Tháng {b.BillingMonth}/{b.BillingYear}", b.OldElectricIndex, b.NewElectricIndex, eUsed, eCost, b.OldWaterIndex, b.NewWaterIndex, wUsed, wCost, b.TotalAmount, status));
            }

            byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
            string fileName = $"BaoCao_HoaDonDienNuoc_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            return File(bytes, "text/csv; charset=utf-8", fileName);
        }

        // Kiem tra chung cho Lap / Sua hoa don
        private void ValidateBill(UtilityBills bill)
        {
            if (bill.OldElectricIndex < 0 || bill.OldWaterIndex < 0)
            {
                ModelState.AddModelError("OldElectricIndex", "Chỉ số công tơ không được âm!");
            }
            if (bill.NewElectricIndex < bill.OldElectricIndex)
            {
                ModelState.AddModelError("NewElectricIndex", "Chỉ số điện mới không được nhỏ hơn chỉ số cũ!");
            }
            if (bill.NewWaterIndex < bill.OldWaterIndex)
            {
                ModelState.AddModelError("NewWaterIndex", "Chỉ số nước mới không được nhỏ hơn chỉ số cũ!");
            }
            if (db.Rooms.Find(bill.RoomId) == null)
            {
                ModelState.AddModelError("RoomId", "Vui lòng chọn phòng hợp lệ!");
            }

            bool duplicateBill = db.UtilityBills.Any(b => b.RoomId == bill.RoomId
                                                        && b.BillingMonth == bill.BillingMonth
                                                        && b.BillingYear == bill.BillingYear
                                                        && b.BillId != bill.BillId);
            if (duplicateBill)
            {
                ModelState.AddModelError("BillingMonth", $"Phòng này đã có hóa đơn tháng {bill.BillingMonth}/{bill.BillingYear} rồi!");
            }
        }

        // GET: UtilityBills/Create
        public ActionResult Create()
        {
            ViewBag.RoomId = new SelectList(db.Rooms.OrderBy(r => r.RoomNumber), "RoomId", "RoomNumber");
            SetPriceViewBag(DateTime.Today.Year, DateTime.Today.Month);
            return View();
        }

        // POST: UtilityBills/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "BillId,RoomId,BillingMonth,BillingYear,OldElectricIndex,NewElectricIndex,OldWaterIndex,NewWaterIndex,TotalAmount,IsPaid")] UtilityBills bill)
        {
            ValidateBill(bill);

            if (ModelState.IsValid)
            {
                ApplyPrice(bill);
                bill.IsPaid = false;
                bill.PaidDate = null;

                db.UtilityBills.Add(bill);
                db.SaveChanges();
                TempData["Success"] = $"Lập hóa đơn điện nước tháng {bill.BillingMonth}/{bill.BillingYear} thành công!";
                return RedirectToAction("Index");
            }

            ViewBag.RoomId = new SelectList(db.Rooms.OrderBy(r => r.RoomNumber), "RoomId", "RoomNumber", bill.RoomId);
            if (bill.BillingMonth >= 1 && bill.BillingMonth <= 12 && bill.BillingYear >= 2000) SetPriceViewBag(bill.BillingYear, bill.BillingMonth);
            else SetPriceViewBag(DateTime.Today.Year, DateTime.Today.Month);
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
                // Danh dau da thu + ghi but toan vao So quy trong cung 1 transaction
                try
                {
                    using (var tx = db.Database.BeginTransaction())
                    using (var shared = DormExtContext.ShareConnection(db))
                    {
                        shared.Database.UseTransaction(tx.UnderlyingTransaction);

                        bill.IsPaid = true;
                        bill.PaidDate = DateTime.Now;
                        db.SaveChanges();

                        if (bill.TotalAmount > 0)
                        {
                            shared.CashFlowTransactions.Add(new CashFlowTransaction
                            {
                                TransactionDate = DateTime.Now,
                                Type = DormConstants.GiaoDichThu,
                                Category = DormConstants.KhoanDienNuoc,
                                Amount = bill.TotalAmount,
                                RelatedPerson = $"Phòng {bill.Rooms.RoomNumber}",
                                RoomNumber = bill.Rooms.RoomNumber,
                                Description = $"Thu tiền điện nước tháng {bill.BillingMonth}/{bill.BillingYear} phòng {bill.Rooms.RoomNumber}",
                                PaymentMethod = paymentMethod,
                                CreatedBy = FinanceService.CurrentUserName(Session),
                                UtilityBillId = bill.BillId
                            });
                            shared.SaveChanges();
                        }
                        tx.Commit();
                    }
                }
                catch (DbUpdateException)
                {
                    // UX_CashFlow_UtilityBill_Active: hoa don vua duoc thu boi request khac -> transaction da rollback
                    TempData["Error"] = "Hóa đơn này vừa được thu rồi, không ghi trùng phiếu thu!";
                    return RedirectToAction("Index");
                }
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
            if (utilityBills.IsPaid)
            {
                TempData["Error"] = "Hóa đơn đã thanh toán (đã ghi sổ quỹ) nên không được sửa!";
                return RedirectToAction("Index");
            }

            ViewBag.RoomId = new SelectList(db.Rooms.OrderBy(r => r.RoomNumber), "RoomId", "RoomNumber", utilityBills.RoomId);
            SetPriceViewBag(utilityBills.BillingYear, utilityBills.BillingMonth);
            return View(utilityBills);
        }

        // POST: UtilityBills/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "BillId,RoomId,BillingMonth,BillingYear,OldElectricIndex,NewElectricIndex,OldWaterIndex,NewWaterIndex")] UtilityBills utilityBills)
        {
            var existing = db.UtilityBills.Find(utilityBills.BillId);
            if (existing == null) return HttpNotFound();
            if (existing.IsPaid)
            {
                TempData["Error"] = "Hóa đơn đã thanh toán (đã ghi sổ quỹ) nên không được sửa!";
                return RedirectToAction("Index");
            }

            ValidateBill(utilityBills);

            if (ModelState.IsValid)
            {
                // Trang thai thanh toan chi doi qua chuc nang Thu tien (PayBill)
                existing.RoomId = utilityBills.RoomId;
                existing.BillingMonth = utilityBills.BillingMonth;
                existing.BillingYear = utilityBills.BillingYear;
                existing.OldElectricIndex = utilityBills.OldElectricIndex;
                existing.NewElectricIndex = utilityBills.NewElectricIndex;
                existing.OldWaterIndex = utilityBills.OldWaterIndex;
                existing.NewWaterIndex = utilityBills.NewWaterIndex;
                ApplyPrice(existing); // tinh lai theo don gia cua ky hoa don
                db.SaveChanges();
                TempData["Success"] = "Cập nhật hóa đơn điện nước thành công!";
                return RedirectToAction("Index");
            }
            ViewBag.RoomId = new SelectList(db.Rooms.OrderBy(r => r.RoomNumber), "RoomId", "RoomNumber", utilityBills.RoomId);
            SetPriceViewBag(existing.BillingYear, existing.BillingMonth);
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
            if (utilityBills == null) return HttpNotFound();
            if (utilityBills.IsPaid)
            {
                TempData["Error"] = "Không thể xóa hóa đơn đã thanh toán (đã ghi sổ quỹ)!";
                return RedirectToAction("Index");
            }
            if (ext.CashFlowTransactions.Any(t => t.UtilityBillId == id))
            {
                TempData["Error"] = "Hóa đơn đã có lịch sử phiếu thu (đã hủy) trong sổ quỹ nên không thể xóa!";
                return RedirectToAction("Index");
            }
            db.UtilityBills.Remove(utilityBills);
            db.SaveChanges();
            TempData["Success"] = "Xóa hóa đơn điện nước thành công!";
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
