using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Qlykytucxa.Models
{
    // Bản ghi thu tiền phòng của sinh viên (mỗi sinh viên một khoản / tháng)
    public class RoomFeePayment
    {
        public int Id { get; set; }
        public int RegistrationId { get; set; }
        public int StudentId { get; set; }
        public string StudentCode { get; set; }
        public string StudentName { get; set; }
        public int RoomId { get; set; }
        public string RoomNumber { get; set; }
        public int FeeMonth { get; set; }
        public int FeeYear { get; set; }
        public decimal Amount { get; set; }
        public string SemesterPeriod { get; set; } // Nhãn kỳ thu, ví dụ: "Tháng 10/2026"
        public DateTime DueDate { get; set; }
        public DateTime? PaidDate { get; set; }
        public string PaymentMethod { get; set; } // "Chuyển khoản VietQR", "Tiền mặt"
        public bool IsPaid { get; set; }
        public string CollectedBy { get; set; }   // Người thu tiền
        public string Note { get; set; }

        // Quá hạn = chưa nộp và đã qua hạn nộp
        public bool IsOverdue { get { return !IsPaid && DueDate.Date < DateTime.Today; } }
        public int DaysOverdue { get { return IsOverdue ? (DateTime.Today - DueDate.Date).Days : 0; } }
    }

    // Sổ quỹ Thu - Chi tổng hợp KTX
    public class CashFlowTransaction
    {
        public int Id { get; set; }
        public DateTime TransactionDate { get; set; }
        public string Type { get; set; } // "Thu" hoặc "Chi"
        public string Category { get; set; } // "Tiền phòng", "Tiền điện nước", "Chi bảo trì", "Chi mua sắm vật tư", "Chi phí khác"
        public decimal Amount { get; set; }
        public string RelatedPerson { get; set; } // Người nộp / Người nhận tiền
        public string RoomNumber { get; set; }
        public string Description { get; set; }
        public string PaymentMethod { get; set; } // "Tiền mặt", "Chuyển khoản"
        public string CreatedBy { get; set; }     // Người ghi sổ

        // Liên kết tới khoản gốc (để hủy phiếu thì trả khoản gốc về trạng thái chưa thu)
        public int? RoomFeePaymentId { get; set; }
        public int? UtilityBillId { get; set; }
        public int? DepositId { get; set; }

        // Phiếu bị hủy vẫn giữ lại để đối chiếu nhưng không tính vào báo cáo
        public bool IsVoided { get; set; }
        public DateTime? VoidedAt { get; set; }
        public string VoidedBy { get; set; }
        public string VoidReason { get; set; }
    }

    // Tiền đặt cọc giữ chỗ (khoản giữ hộ, không phải doanh thu; phần khấu trừ khi hoàn mới là doanh thu)
    public class Deposit
    {
        public int Id { get; set; }
        public int RegistrationId { get; set; }
        public int StudentId { get; set; }
        public string StudentCode { get; set; }
        public string StudentName { get; set; }
        public string RoomNumber { get; set; }
        public decimal Amount { get; set; }
        public DateTime CollectedDate { get; set; }
        public string PaymentMethod { get; set; }
        public string CollectedBy { get; set; }
        public string Status { get; set; } // "Đang giữ", "Đã hoàn", "Đã hủy"
        public DateTime? RefundedDate { get; set; }
        public decimal? RefundAmount { get; set; }
        public decimal? DeductionAmount { get; set; }
        public string DeductionReason { get; set; }
        public string RefundedBy { get; set; }
        public string Note { get; set; }
    }

    // Bảng giá điện nước theo thời gian: áp dụng cho hóa đơn có kỳ (tháng) từ EffectiveFrom trở đi
    public class UtilityPrice
    {
        public int Id { get; set; }
        public DateTime EffectiveFrom { get; set; } // luôn là ngày 1 của tháng
        public decimal ElectricPrice { get; set; }  // đ/kWh
        public decimal WaterPrice { get; set; }     // đ/m3
        public string Note { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // Dòng công nợ quá hạn (tiền phòng hoặc điện nước)
    public class OverdueItem
    {
        public string Kind { get; set; }        // "Tiền phòng" / "Tiền điện nước"
        public int SourceId { get; set; }
        public string Payer { get; set; }       // Sinh viên hoặc phòng
        public string SearchKey { get; set; }   // Mã SV / số phòng để mở trang thu tiền
        public string RoomNumber { get; set; }
        public string Period { get; set; }
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
        public int DaysOverdue { get { return (DateTime.Today - DueDate.Date).Days; } }
    }

    // ViewModel cho trang Báo cáo Tài chính
    public class FinanceReportViewModel
    {
        public decimal TotalRevenue { get; set; }   // Tổng Thu thực tế
        public decimal TotalExpense { get; set; }   // Tổng Chi thực tế
        public decimal NetProfit { get; set; }      // Lợi nhuận ròng = Thu - Chi
        public decimal TotalReceivable { get; set; } // Công nợ chưa thu (tiền phòng nợ + điện nước nợ)

        public decimal RoomFeeRevenue { get; set; } // Thu từ tiền phòng
        public decimal UtilityRevenue { get; set; } // Thu từ điện nước

        public decimal DepositDeductionRevenue { get; set; } // Khấu trừ tiền cọc (tính là doanh thu)
        public decimal DepositsHeld { get; set; }    // Tiền cọc đang giữ hộ sinh viên
        public decimal OverdueAmount { get; set; }   // Công nợ đã quá hạn
        public int OverdueCount { get; set; }

        public List<CashFlowTransaction> RecentTransactions { get; set; }
        public List<RoomFeePayment> PendingRoomFees { get; set; }
    }
}
