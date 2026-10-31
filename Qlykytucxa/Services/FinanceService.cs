using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using Qlykytucxa.Models;

namespace Qlykytucxa.Services
{
    public static class FinanceService
    {
        // Ten nguoi dang dang nhap, ghi vao so quy / phieu thu
        public static string CurrentUserName(HttpSessionStateBase session)
        {
            var user = UserSession.FromSession(session);
            if (user == null) return "Không rõ";
            return string.IsNullOrEmpty(user.FullName) ? user.Username : user.FullName;
        }

        // Han nop tien dien nuoc: ngay 15 cua thang ke tiep ky hoa don
        public static DateTime UtilityDueDate(int billingYear, int billingMonth)
        {
            return new DateTime(billingYear, billingMonth, 1).AddMonths(1).AddDays(14);
        }

        // Don gia dien nuoc ap dung cho ky hoa don (thang/nam): ban gia moi nhat co hieu luc tu truoc ky do
        public static UtilityPrice GetUtilityPrice(DormExtContext ext, int billingYear, int billingMonth)
        {
            var periodStart = new DateTime(billingYear, billingMonth, 1);
            return ext.UtilityPrices.Where(p => p.EffectiveFrom <= periodStart)
                                    .OrderByDescending(p => p.EffectiveFrom)
                                    .FirstOrDefault()
                   ?? new UtilityPrice
                   {
                       EffectiveFrom = new DateTime(2000, 1, 1),
                       ElectricPrice = DormConstants.ElectricPrice,
                       WaterPrice = DormConstants.WaterPrice
                   };
        }

        // Tat ca khoan tien phong + dien nuoc chua thu va da qua han nop
        public static List<OverdueItem> GetOverdueItems(MiniDormDBEntities db, DormExtContext ext)
        {
            DateTime today = DateTime.Today;

            var items = ext.RoomFeePayments.Where(f => !f.IsPaid && f.DueDate < today).ToList()
                .Select(f => new OverdueItem
                {
                    Kind = DormConstants.KhoanTienPhong,
                    SourceId = f.Id,
                    Payer = $"{f.StudentName} ({f.StudentCode})",
                    SearchKey = f.StudentCode,
                    RoomNumber = f.RoomNumber,
                    Period = f.SemesterPeriod,
                    Amount = f.Amount,
                    DueDate = f.DueDate
                }).ToList();

            foreach (var b in db.UtilityBills.Include(b => b.Rooms).Where(b => !b.IsPaid && b.TotalAmount > 0).ToList())
            {
                DateTime due = UtilityDueDate(b.BillingYear, b.BillingMonth);
                if (due >= today) continue;
                items.Add(new OverdueItem
                {
                    Kind = DormConstants.KhoanDienNuoc,
                    SourceId = b.BillId,
                    Payer = "Phòng " + b.Rooms.RoomNumber,
                    SearchKey = b.Rooms.RoomNumber,
                    RoomNumber = b.Rooms.RoomNumber,
                    Period = $"Tháng {b.BillingMonth:D2}/{b.BillingYear}",
                    Amount = b.TotalAmount,
                    DueDate = due
                });
            }
            return items.OrderBy(i => i.DueDate).ToList();
        }

        // Huy 1 phieu trong so quy va tra khoan goc ve trang thai truoc khi thu.
        // tx phai duoc nap tu context 'shared' (dung chung transaction voi db). Tra ve thong bao loi, null neu hop le.
        // Nguoi goi tu SaveChanges + Commit.
        public static string VoidTransaction(MiniDormDBEntities db, DormExtContext shared, CashFlowTransaction tx, string reason, string userName)
        {
            if (tx.IsVoided) return "Phiếu này đã bị hủy trước đó!";
            if (string.IsNullOrWhiteSpace(reason)) return "Vui lòng nhập lý do hủy phiếu!";

            if (tx.RoomFeePaymentId.HasValue)
            {
                var fee = shared.RoomFeePayments.Find(tx.RoomFeePaymentId.Value);
                if (fee != null)
                {
                    fee.IsPaid = false;
                    fee.PaidDate = null;
                    fee.PaymentMethod = null;
                    fee.CollectedBy = null;
                }
            }

            if (tx.UtilityBillId.HasValue)
            {
                var bill = db.UtilityBills.Find(tx.UtilityBillId.Value);
                if (bill != null)
                {
                    bill.IsPaid = false;
                    bill.PaidDate = null;
                }
            }

            if (tx.DepositId.HasValue)
            {
                var deposit = shared.Deposits.Find(tx.DepositId.Value);
                if (deposit != null)
                {
                    if (tx.Category == DormConstants.KhoanTienCoc)
                    {
                        if (deposit.Status == DormConstants.CocDaHoan)
                        {
                            return "Khoản cọc này đã được hoàn, hãy hủy phiếu hoàn cọc trước!";
                        }
                        deposit.Status = DormConstants.CocDaHuy;
                    }
                    else if (tx.Category == DormConstants.KhoanHoanCoc)
                    {
                        ResetRefund(deposit);
                    }
                }
            }

            tx.IsVoided = true;
            tx.VoidedAt = DateTime.Now;
            tx.VoidedBy = userName;
            tx.VoidReason = reason.Trim();
            return null;
        }

        // Dua khoan coc ve trang thai "Đang giữ" (huy viec hoan coc)
        public static void ResetRefund(Deposit deposit)
        {
            deposit.Status = DormConstants.CocDangGiu;
            deposit.RefundedDate = null;
            deposit.RefundAmount = null;
            deposit.DeductionAmount = null;
            deposit.DeductionReason = null;
            deposit.RefundedBy = null;
        }
    }
}
