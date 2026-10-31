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
        public string Note { get; set; }
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

        public List<CashFlowTransaction> RecentTransactions { get; set; }
        public List<RoomFeePayment> PendingRoomFees { get; set; }
    }
}
