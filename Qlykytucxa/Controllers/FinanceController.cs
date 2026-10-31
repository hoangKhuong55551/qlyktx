using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Net;
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
        // Khoan muc tien coc: la tien giu ho, khong tinh vao doanh thu / chi phi
        private static readonly string[] DepositCategories = { DormConstants.KhoanTienCoc, DormConstants.KhoanHoanCoc };

        private MiniDormDBEntities db = new MiniDormDBEntities();
        private DormExtContext ext = new DormExtContext();

        private string CurrentUser
        {
            get { return FinanceService.CurrentUserName(Session); }
        }

        // Chay thao tac ghi tren ca 2 context (EDMX + bang mo rong) trong cung 1 transaction.
        // work tra ve thong bao loi (khong luu gi) hoac null (luu va commit).
        private string RunInTransaction(Func<DormExtContext, string> work)
        {
            using (var tx = db.Database.BeginTransaction())
            using (var shared = DormExtContext.ShareConnection(db))
            {
                shared.Database.UseTransaction(tx.UnderlyingTransaction);
                string error = work(shared);
                if (error != null) return error;

                shared.SaveChanges();
                db.SaveChanges();
                tx.Commit();
                return null;
            }
        }

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

            // Chỉ tính các phiếu còn hiệu lực (không tính phiếu đã hủy)
            var validTx = ext.CashFlowTransactions.Where(t => !t.IsVoided);

            // 1. Thống kê Doanh thu
            decimal paidRoomFees = ext.RoomFeePayments.Where(f => f.IsPaid).Sum(f => (decimal?)f.Amount) ?? 0;
            decimal paidUtilities = db.UtilityBills.Where(b => b.IsPaid).Sum(b => (decimal?)b.TotalAmount) ?? 0;
            decimal otherRevenue = validTx
                .Where(t => t.Type == DormConstants.GiaoDichThu
                         && t.Category != DormConstants.KhoanTienPhong
                         && t.Category != DormConstants.KhoanDienNuoc
                         && !DepositCategories.Contains(t.Category))
                .Sum(t => (decimal?)t.Amount) ?? 0;
            decimal depositDeductions = ext.Deposits.Where(d => d.Status == DormConstants.CocDaHoan).Sum(d => d.DeductionAmount) ?? 0;

            decimal totalRevenue = paidRoomFees + paidUtilities + otherRevenue + depositDeductions;

            // 2. Thống kê Chi phí (hoàn cọc không phải chi phí)
            decimal totalExpense = validTx
                .Where(t => t.Type == DormConstants.GiaoDichChi && !DepositCategories.Contains(t.Category))
                .Sum(t => (decimal?)t.Amount) ?? 0;

            // 3. Công nợ cần thu và công nợ quá hạn
            decimal unpaidRoomFees = ext.RoomFeePayments.Where(f => !f.IsPaid).Sum(f => (decimal?)f.Amount) ?? 0;
            decimal unpaidUtilities = db.UtilityBills.Where(b => !b.IsPaid).Sum(b => (decimal?)b.TotalAmount) ?? 0;
            var overdue = FinanceService.GetOverdueItems(db, ext);

            // 4. Biểu đồ Thu - Chi 6 tháng gần nhất (theo sổ quỹ, bỏ phiếu hủy và tiền cọc; cộng khấu trừ cọc vào thu)
            var chartLabels = new List<string>();
            var chartRevenue = new List<decimal>();
            var chartExpense = new List<decimal>();
            var chartProfit = new List<decimal>();

            var fromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-5);
            var recentTx = validTx.Where(t => t.TransactionDate >= fromDate && !DepositCategories.Contains(t.Category)).ToList();
            var recentDeductions = ext.Deposits.Where(d => d.Status == DormConstants.CocDaHoan && d.RefundedDate >= fromDate && d.DeductionAmount > 0).ToList();

            for (int i = 5; i >= 0; i--)
            {
                var dt = DateTime.Now.AddMonths(-i);
                chartLabels.Add($"T{dt.Month}/{dt.Year}");

                decimal mRev = recentTx.Where(t => t.Type == DormConstants.GiaoDichThu && t.TransactionDate.Month == dt.Month && t.TransactionDate.Year == dt.Year).Sum(t => t.Amount)
                             + recentDeductions.Where(d => d.RefundedDate.Value.Month == dt.Month && d.RefundedDate.Value.Year == dt.Year).Sum(d => d.DeductionAmount ?? 0);
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
                NetProfit = totalRevenue - totalExpense,
                TotalReceivable = unpaidRoomFees + unpaidUtilities,
                RoomFeeRevenue = paidRoomFees,
                UtilityRevenue = paidUtilities,
                DepositDeductionRevenue = depositDeductions,
                DepositsHeld = ext.Deposits.Where(d => d.Status == DormConstants.CocDangGiu).Sum(d => (decimal?)d.Amount) ?? 0,
                OverdueAmount = overdue.Sum(o => o.Amount),
                OverdueCount = overdue.Count,
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

            DateTime today = DateTime.Today;
            if (statusFilter == "paid")
            {
                list = list.Where(f => f.IsPaid);
            }
            else if (statusFilter == "unpaid")
            {
                list = list.Where(f => !f.IsPaid);
            }
            else if (statusFilter == "overdue")
            {
                list = list.Where(f => !f.IsPaid && f.DueDate < today);
            }

            ViewBag.SearchString = searchString;
            ViewBag.StatusFilter = statusFilter;

            return View(list.OrderBy(f => f.IsPaid).ThenBy(f => f.DueDate).ToList());
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
                fee.CollectedBy = CurrentUser;

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
                    PaymentMethod = paymentMethod,
                    CreatedBy = CurrentUser,
                    RoomFeePaymentId = fee.Id
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
                PaymentMethod = paymentMethod,
                CreatedBy = CurrentUser
            });
            ext.SaveChanges();

            TempData["Success"] = $"Đã ghi nhận khoản chi {amount:N0} đ ({category}) vào sổ quỹ!";
            return RedirectToAction("Index");
        }

        // POST: Finance/VoidTransaction - Hủy phiếu thu/chi nhập sai (giữ lại lịch sử, khoản gốc trở về chưa thu)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VoidTransaction(int id, string reason)
        {
            string error = RunInTransaction(shared =>
            {
                var tx = shared.CashFlowTransactions.Find(id);
                if (tx == null) return "Không tìm thấy phiếu!";
                return FinanceService.VoidTransaction(db, shared, tx, reason, CurrentUser);
            });

            if (error != null) TempData["Error"] = error;
            else TempData["Success"] = $"Đã hủy phiếu GD#{id:D4}. Khoản gốc (nếu có) đã trở về trạng thái chưa thu.";
            return RedirectToAction("Index");
        }

        // GET: Finance/PrintRoomReceipt/5 (Mẫu in Phiếu thu tiền phòng KTX chuẩn A5 & VietQR)
        // Sinh viên được xem phiếu của chính mình
        [RoleAuthorize(UserRoles.Admin, UserRoles.Student)]
        public ActionResult PrintRoomReceipt(int id)
        {
            var fee = ext.RoomFeePayments.Find(id);
            if (fee == null) return HttpNotFound();

            var user = UserSession.FromSession(Session);
            if (user.Role == UserRoles.Student && fee.StudentId != user.StudentId)
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
            }
            return View(fee);
        }

        // GET: Finance/ExportFinanceExcel (Xuất sổ quỹ thu chi ra file Excel CSV UTF-8)
        public ActionResult ExportFinanceExcel(string typeFilter, int? monthFilter, int? yearFilter)
        {
            var items = FilterTransactions(typeFilter, monthFilter, yearFilter).OrderByDescending(t => t.TransactionDate).ToList();

            var sb = new StringBuilder();
            sb.Append('﻿'); // UTF-8 BOM
            sb.AppendLine("Mã GD,Ngày giao dịch,Loại (Thu/Chi),Khoản mục,Số tiền (VNĐ),Người nộp/Người nhận,Phòng liên quan,Hình thức,Nội dung chi tiết,Người ghi sổ,Trạng thái,Lý do hủy");

            foreach (var t in items)
            {
                sb.AppendLine(CsvHelper.Row($"GD{t.Id:D4}", t.TransactionDate.ToString("dd/MM/yyyy HH:mm"), t.Type, t.Category, t.Amount,
                    t.RelatedPerson, t.RoomNumber, t.PaymentMethod, t.Description, t.CreatedBy,
                    t.IsVoided ? "Đã hủy" : "Hiệu lực", t.VoidReason));
            }

            byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
            string fileName = $"SoQuy_ThuChi_KTX_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            return File(bytes, "text/csv; charset=utf-8", fileName);
        }

        #region Công nợ quá hạn
        // GET: Finance/Debts
        public ActionResult Debts()
        {
            RoomFeeService.EnsureMonthlyFees(db, ext);
            return View(FinanceService.GetOverdueItems(db, ext));
        }
        #endregion

        #region Tiền đặt cọc
        // GET: Finance/Deposits
        public ActionResult Deposits()
        {
            var deposits = ext.Deposits.OrderByDescending(d => d.CollectedDate).ToList();

            // Trạng thái hợp đồng của từng khoản cọc (để nhắc hoàn cọc khi SV đã rời KTX)
            var regIds = deposits.Select(d => d.RegistrationId).Distinct().ToList();
            ViewBag.RegistrationStatus = db.RoomRegistrations.Where(r => regIds.Contains(r.RegistrationId))
                                           .ToDictionary(r => r.RegistrationId, r => r.Status);

            // Hợp đồng đang ở nhưng chưa thu cọc
            var activeDepositRegIds = ext.Deposits.Where(d => d.Status != DormConstants.CocDaHuy).Select(d => d.RegistrationId).ToList();
            ViewBag.PendingRegistrations = db.RoomRegistrations.Include("Students").Include("Rooms")
                .Where(r => r.Status == DormConstants.StatusDangO && !activeDepositRegIds.Contains(r.RegistrationId))
                .OrderBy(r => r.Rooms.RoomNumber)
                .ToList();

            return View(deposits);
        }

        // POST: Finance/CollectDeposit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CollectDeposit(int registrationId, decimal amount, string paymentMethod = "Tiền mặt")
        {
            var reg = db.RoomRegistrations.Include("Students").Include("Rooms").FirstOrDefault(r => r.RegistrationId == registrationId);
            string error = null;
            if (reg == null || reg.Status != DormConstants.StatusDangO) error = "Chỉ thu cọc cho hợp đồng đang ở!";
            else if (amount <= 0) error = "Số tiền cọc phải lớn hơn 0!";
            else if (ext.Deposits.Any(d => d.RegistrationId == registrationId && d.Status != DormConstants.CocDaHuy)) error = "Hợp đồng này đã thu cọc rồi!";

            if (error == null)
            {
                try
                {
                    error = RunInTransaction(shared =>
                    {
                        var deposit = new Deposit
                        {
                            RegistrationId = reg.RegistrationId,
                            StudentId = reg.StudentId,
                            StudentCode = reg.Students.StudentCode,
                            StudentName = reg.Students.FullName,
                            RoomNumber = reg.Rooms.RoomNumber,
                            Amount = amount,
                            CollectedDate = DateTime.Now,
                            PaymentMethod = paymentMethod,
                            CollectedBy = CurrentUser,
                            Status = DormConstants.CocDangGiu
                        };
                        shared.Deposits.Add(deposit);
                        shared.SaveChanges(); // lấy Id để liên kết phiếu thu

                        shared.CashFlowTransactions.Add(new CashFlowTransaction
                        {
                            TransactionDate = DateTime.Now,
                            Type = DormConstants.GiaoDichThu,
                            Category = DormConstants.KhoanTienCoc,
                            Amount = amount,
                            RelatedPerson = $"{deposit.StudentName} ({deposit.StudentCode})",
                            RoomNumber = deposit.RoomNumber,
                            Description = $"Thu tiền đặt cọc giữ chỗ phòng {deposit.RoomNumber}",
                            PaymentMethod = paymentMethod,
                            CreatedBy = CurrentUser,
                            DepositId = deposit.Id
                        });
                        return null;
                    });
                }
                catch (DbUpdateException)
                {
                    error = "Hợp đồng này đã thu cọc rồi!";
                }
            }

            if (error != null) TempData["Error"] = error;
            else TempData["Success"] = $"Đã thu tiền cọc {amount:N0} đ của sinh viên {reg.Students.FullName}.";
            return RedirectToAction("Deposits");
        }

        // POST: Finance/RefundDeposit - Hoàn cọc khi SV rời KTX, có thể khấu trừ (hư hỏng, nợ...)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RefundDeposit(int id, decimal deductionAmount, string deductionReason, string paymentMethod = "Tiền mặt")
        {
            decimal refund = 0;
            string error = RunInTransaction(shared =>
            {
                var deposit = shared.Deposits.Find(id);
                if (deposit == null || deposit.Status != DormConstants.CocDangGiu) return "Khoản cọc không ở trạng thái đang giữ!";

                var reg = db.RoomRegistrations.Find(deposit.RegistrationId);
                if (reg != null && reg.Status == DormConstants.StatusDangO)
                    return "Sinh viên vẫn đang ở. Hãy làm thủ tục trả phòng / thanh lý trước khi hoàn cọc!";
                if (deductionAmount < 0 || deductionAmount > deposit.Amount)
                    return $"Số tiền khấu trừ phải từ 0 đến {deposit.Amount:N0} đ!";
                if (deductionAmount > 0 && string.IsNullOrWhiteSpace(deductionReason))
                    return "Vui lòng ghi lý do khấu trừ tiền cọc!";

                refund = deposit.Amount - deductionAmount;
                deposit.Status = DormConstants.CocDaHoan;
                deposit.RefundedDate = DateTime.Now;
                deposit.RefundAmount = refund;
                deposit.DeductionAmount = deductionAmount;
                deposit.DeductionReason = deductionAmount > 0 ? deductionReason.Trim() : null;
                deposit.RefundedBy = CurrentUser;

                if (refund > 0)
                {
                    shared.CashFlowTransactions.Add(new CashFlowTransaction
                    {
                        TransactionDate = DateTime.Now,
                        Type = DormConstants.GiaoDichChi,
                        Category = DormConstants.KhoanHoanCoc,
                        Amount = refund,
                        RelatedPerson = $"{deposit.StudentName} ({deposit.StudentCode})",
                        RoomNumber = deposit.RoomNumber,
                        Description = deductionAmount > 0
                            ? $"Hoàn cọc phòng {deposit.RoomNumber} (khấu trừ {deductionAmount:N0} đ: {deposit.DeductionReason})"
                            : $"Hoàn trả toàn bộ tiền cọc phòng {deposit.RoomNumber}",
                        PaymentMethod = paymentMethod,
                        CreatedBy = CurrentUser,
                        DepositId = deposit.Id
                    });
                }
                return null;
            });

            if (error != null) TempData["Error"] = error;
            else TempData["Success"] = $"Đã hoàn cọc {refund:N0} đ" + (deductionAmount > 0 ? $" (khấu trừ {deductionAmount:N0} đ)." : ".");
            return RedirectToAction("Deposits");
        }

        // POST: Finance/CancelRefund - Hủy việc hoàn cọc đã ghi nhầm
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CancelRefund(int id, string reason)
        {
            string error = RunInTransaction(shared =>
            {
                var deposit = shared.Deposits.Find(id);
                if (deposit == null || deposit.Status != DormConstants.CocDaHoan) return "Khoản cọc chưa được hoàn!";

                var refundTx = shared.CashFlowTransactions.FirstOrDefault(t => t.DepositId == id && !t.IsVoided && t.Category == DormConstants.KhoanHoanCoc);
                if (refundTx != null)
                {
                    return FinanceService.VoidTransaction(db, shared, refundTx, reason, CurrentUser);
                }
                // Khấu trừ toàn bộ -> không có phiếu chi, chỉ cần đưa về trạng thái đang giữ
                if (string.IsNullOrWhiteSpace(reason)) return "Vui lòng nhập lý do hủy!";
                FinanceService.ResetRefund(deposit);
                return null;
            });

            if (error != null) TempData["Error"] = error;
            else TempData["Success"] = "Đã hủy việc hoàn cọc, khoản cọc trở về trạng thái đang giữ.";
            return RedirectToAction("Deposits");
        }
        #endregion

        #region Bảng giá điện nước
        // GET: Finance/UtilityPrices
        public ActionResult UtilityPrices()
        {
            ViewBag.CurrentPrice = FinanceService.GetUtilityPrice(ext, DateTime.Today.Year, DateTime.Today.Month);
            return View(ext.UtilityPrices.OrderByDescending(p => p.EffectiveFrom).ToList());
        }

        // POST: Finance/AddUtilityPrice - Thêm mức giá mới áp dụng từ 1 kỳ (tháng) hóa đơn
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddUtilityPrice(int month, int year, decimal electricPrice, decimal waterPrice, string note)
        {
            string error = null;
            if (month < 1 || month > 12 || year < 2000 || year > 2100) error = "Kỳ áp dụng không hợp lệ!";
            else if (electricPrice <= 0 || waterPrice <= 0) error = "Đơn giá phải lớn hơn 0!";
            else
            {
                var from = new DateTime(year, month, 1);
                if (ext.UtilityPrices.Any(p => p.EffectiveFrom == from)) error = $"Đã có bảng giá áp dụng từ tháng {month:D2}/{year}!";
                else
                {
                    ext.UtilityPrices.Add(new UtilityPrice
                    {
                        EffectiveFrom = from,
                        ElectricPrice = electricPrice,
                        WaterPrice = waterPrice,
                        Note = note,
                        CreatedBy = CurrentUser,
                        CreatedAt = DateTime.Now
                    });
                    ext.SaveChanges();
                }
            }

            if (error != null) TempData["Error"] = error;
            else TempData["Success"] = $"Đã thêm đơn giá điện {electricPrice:N0} đ/kWh, nước {waterPrice:N0} đ/m³ áp dụng từ kỳ {month:D2}/{year}.";
            return RedirectToAction("UtilityPrices");
        }
        #endregion

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
