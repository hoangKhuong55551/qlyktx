using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using Qlykytucxa.Filters;
using Qlykytucxa.Models;
using Qlykytucxa.Services;

namespace Qlykytucxa.Controllers
{
    [RoleAuthorize(UserRoles.Admin)]
    public class FinanceController : Controller
    {
        private MiniDormDBEntities db = new MiniDormDBEntities();
        private DormExtContext ext = new DormExtContext();

        private IQueryable<CashFlowTransaction> FilterTransactions(string typeFilter, int? monthFilter, int? yearFilter)
        {
            var list = ext.CashFlowTransactions.AsQueryable();
            if (!string.IsNullOrEmpty(typeFilter))
            {
                list = list.Where(t => t.Type == typeFilter);
            }
            if (monthFilter.HasValue && monthFilter.Value > 0)
            {
                list = list.Where(t => t.TransactionDate.Month == monthFilter.Value);
            }
            if (yearFilter.HasValue && yearFilter.Value > 0)
            {
                list = list.Where(t => t.TransactionDate.Year == yearFilter.Value);
            }
            return list;
        }

        // GET: Finance (Báo cáo Thu - Chi tổng thể KTX)
        public ActionResult Index(string typeFilter, int? monthFilter, int? yearFilter)
        {
            RoomFeeService.EnsureMonthlyFees(db, ext);

            // 1. Thống kê Doanh thu
            decimal paidRoomFees = ext.RoomFeePayments.Where(f => f.IsPaid).Sum(f => (decimal?)f.Amount) ?? 0;
            decimal paidUtilities = db.UtilityBills.Where(b => b.IsPaid).Sum(b => (decimal?)b.TotalAmount) ?? 0;
            decimal otherRevenue = ext.CashFlowTransactions
                .Where(t => t.Type == DormConstants.GiaoDichThu && t.Category != DormConstants.KhoanTienPhong && t.Category != DormConstants.KhoanDienNuoc)
                .Sum(t => (decimal?)t.Amount) ?? 0;

            decimal totalRevenue = paidRoomFees + paidUtilities + otherRevenue;

            // 2. Thống kê Chi phí
            decimal totalExpense = ext.CashFlowTransactions.Where(t => t.Type == DormConstants.GiaoDichChi).Sum(t => (decimal?)t.Amount) ?? 0;

            // 3. Lợi nhuận ròng
            decimal netProfit = totalRevenue - totalExpense;

            // 4. Công nợ cần thu
            decimal unpaidRoomFees = ext.RoomFeePayments.Where(f => !f.IsPaid).Sum(f => (decimal?)f.Amount) ?? 0;
            decimal unpaidUtilities = db.UtilityBills.Where(b => !b.IsPaid).Sum(b => (decimal?)b.TotalAmount) ?? 0;
            decimal totalReceivable = unpaidRoomFees + unpaidUtilities;

            // 5. Chuẩn bị dữ liệu biểu đồ Thu - Chi qua các tháng gần đây
            var chartLabels = new List<string>();
            var chartRevenue = new List<decimal>();
            var chartExpense = new List<decimal>();
            var chartProfit = new List<decimal>();

            var fromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-5);
            var recentTx = ext.CashFlowTransactions.Where(t => t.TransactionDate >= fromDate).ToList();

            for (int i = 5; i >= 0; i--)
            {
                var dt = DateTime.Now.AddMonths(-i);
                chartLabels.Add($"T{dt.Month}/{dt.Year}");

                decimal mRev = recentTx.Where(t => t.Type == DormConstants.GiaoDichThu && t.TransactionDate.Month == dt.Month && t.TransactionDate.Year == dt.Year).Sum(t => t.Amount);
                decimal mExp = recentTx.Where(t => t.Type == DormConstants.GiaoDichChi && t.TransactionDate.Month == dt.Month && t.TransactionDate.Year == dt.Year).Sum(t => t.Amount);

                chartRevenue.Add(mRev);
                chartExpense.Add(mExp);
                chartProfit.Add(mRev - mExp);
            }

            ViewBag.ChartLabels = chartLabels.ToArray();
            ViewBag.ChartRevenue = chartRevenue.ToArray();
            ViewBag.ChartExpense = chartExpense.ToArray();
            ViewBag.ChartProfit = chartProfit.ToArray();

            ViewBag.TypeFilter = typeFilter;
            ViewBag.MonthFilter = monthFilter;
            ViewBag.YearFilter = yearFilter;

            var model = new FinanceReportViewModel
            {
                TotalRevenue = totalRevenue,
                TotalExpense = totalExpense,
                NetProfit = netProfit,
                TotalReceivable = totalReceivable,
                RoomFeeRevenue = paidRoomFees,
                UtilityRevenue = paidUtilities,
                RecentTransactions = FilterTransactions(typeFilter, monthFilter, yearFilter)
                    .OrderByDescending(t => t.TransactionDate).Take(15).ToList(),
                PendingRoomFees = ext.RoomFeePayments.Where(f => !f.IsPaid).OrderBy(f => f.DueDate).Take(5).ToList()
            };

            return View(model);
        }

        // GET: Finance/RoomFees (Quản lý Thu Tiền Phòng KTX)
        public ActionResult RoomFees(string searchString, string statusFilter)
        {
            RoomFeeService.EnsureMonthlyFees(db, ext);
            var list = ext.RoomFeePayments.AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                list = list.Where(f => f.StudentCode.Contains(searchString)
                                    || f.StudentName.Contains(searchString)
                                    || f.RoomNumber.Contains(searchString));
            }

            if (statusFilter == "paid")
            {
                list = list.Where(f => f.IsPaid);
            }
            else if (statusFilter == "unpaid")
            {
                list = list.Where(f => !f.IsPaid);
            }

            ViewBag.SearchString = searchString;
            ViewBag.StatusFilter = statusFilter;

            return View(list.OrderBy(f => f.IsPaid).ThenByDescending(f => f.DueDate).ToList());
        }

        // POST: Finance/CollectRoomFee (Thu tiền phòng cho sinh viên)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CollectRoomFee(int id, string paymentMethod = "Tiền mặt")
        {
            var fee = ext.RoomFeePayments.Find(id);
            if (fee != null && !fee.IsPaid)
            {
                fee.IsPaid = true;
                fee.PaidDate = DateTime.Now;
                fee.PaymentMethod = paymentMethod;

                // Ghi tự động 1 bút toán vào Sổ quỹ Thu (lưu cùng 1 lần SaveChanges -> cùng transaction)
                ext.CashFlowTransactions.Add(new CashFlowTransaction
                {
                    TransactionDate = DateTime.Now,
                    Type = DormConstants.GiaoDichThu,
                    Category = DormConstants.KhoanTienPhong,
                    Amount = fee.Amount,
                    RelatedPerson = $"{fee.StudentName} ({fee.StudentCode})",
                    RoomNumber = fee.RoomNumber,
                    Description = $"Thu tiền phòng {fee.SemesterPeriod} của sinh viên {fee.StudentName}",
                    PaymentMethod = paymentMethod
                });
                ext.SaveChanges();

                TempData["Success"] = $"Đã xác nhận thu tiền phòng {fee.Amount:N0} đ từ sinh viên {fee.StudentName} (Phòng {fee.RoomNumber}) thành công!";
            }
            return RedirectToAction("RoomFees");
        }

        // POST: Finance/AddExpense (Ghi nhận một khoản chi phí vận hành KTX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddExpense(string category, decimal amount, string relatedPerson, string description, string paymentMethod = "Tiền mặt")
        {
            if (amount <= 0)
            {
                TempData["Error"] = "Số tiền chi phải lớn hơn 0!";
                return RedirectToAction("Index");
            }

            ext.CashFlowTransactions.Add(new CashFlowTransaction
            {
                TransactionDate = DateTime.Now,
                Type = DormConstants.GiaoDichChi,
                Category = string.IsNullOrEmpty(category) ? "Chi phí vận hành" : category,
                Amount = amount,
                RelatedPerson = relatedPerson ?? "Nhân viên phụ trách",
                RoomNumber = "Toàn KTX",
                Description = description ?? "Khoản chi bảo trì, sửa chữa, vận hành ký túc xá",
                PaymentMethod = paymentMethod
            });
            ext.SaveChanges();

            TempData["Success"] = $"Đã ghi nhận khoản chi {amount:N0} đ ({category}) vào sổ quỹ!";
            return RedirectToAction("Index");
        }

        // GET: Finance/PrintRoomReceipt/5 (Mẫu in Phiếu thu tiền phòng KTX chuẩn A5 & VietQR)
        public ActionResult PrintRoomReceipt(int id)
        {
            var fee = ext.RoomFeePayments.Find(id);
            if (fee == null) return HttpNotFound();

            return View(fee);
        }

        // GET: Finance/ExportFinanceExcel (Xuất sổ quỹ thu chi ra file Excel CSV UTF-8)
        public ActionResult ExportFinanceExcel(string typeFilter, int? monthFilter, int? yearFilter)
        {
            var items = FilterTransactions(typeFilter, monthFilter, yearFilter).OrderByDescending(t => t.TransactionDate).ToList();

            var sb = new StringBuilder();
            sb.Append('﻿'); // UTF-8 BOM
            sb.AppendLine("Mã GD,Ngày giao dịch,Loại (Thu/Chi),Khoản mục,Số tiền (VNĐ),Người nộp/Người nhận,Phòng liên quan,Hình thức,Nội dung chi tiết");

            foreach (var t in items)
            {
                sb.AppendLine(CsvHelper.Row($"GD{t.Id:D4}", t.TransactionDate.ToString("dd/MM/yyyy HH:mm"), t.Type, t.Category, t.Amount, t.RelatedPerson, t.RoomNumber, t.PaymentMethod, t.Description));
            }

            byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
            string fileName = $"SoQuy_ThuChi_KTX_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            return File(bytes, "text/csv; charset=utf-8", fileName);
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
