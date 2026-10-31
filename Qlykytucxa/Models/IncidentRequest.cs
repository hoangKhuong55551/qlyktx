using System;
using System.ComponentModel.DataAnnotations;

namespace Qlykytucxa.Models
{
    public class IncidentRequest
    {
        public int Id { get; set; }

        [Display(Name = "Phòng")]
        [Required(ErrorMessage = "Vui lòng chọn phòng phát sinh sự cố")]
        public int RoomId { get; set; }

        [Display(Name = "Số phòng")]
        public string RoomNumber { get; set; }

        [Display(Name = "Tiêu đề sự cố")]
        [Required(ErrorMessage = "Vui lòng nhập tiêu đề sự cố")]
        public string Title { get; set; }

        [Display(Name = "Loại sự cố")]
        [Required(ErrorMessage = "Vui lòng chọn loại sự cố")]
        public string Category { get; set; } // Điện, Nước, Điều hòa, Giường tủ, Thiết bị khác

        [Display(Name = "Mô tả chi tiết")]
        [Required(ErrorMessage = "Vui lòng mô tả chi tiết hư hỏng")]
        public string Description { get; set; }

        [Display(Name = "Người báo hỏng")]
        [Required(ErrorMessage = "Vui lòng nhập tên người báo")]
        public string ReporterName { get; set; }

        [Display(Name = "Ngày báo")]
        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm}")]
        public DateTime CreatedAt { get; set; }

        [Display(Name = "Nhân viên xử lý")]
        public string AssignedTo { get; set; }

        [Display(Name = "Trạng thái")]
        public string Status { get; set; } // Mới tiếp nhận, Đang xử lý, Đã hoàn thành

        [Display(Name = "Chi phí sửa chữa (VNĐ)")]
        [DisplayFormat(DataFormatString = "{0:N0} đ")]
        public decimal Cost { get; set; }

        [Display(Name = "Ngày hoàn thành")]
        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm}")]
        public DateTime? ResolvedAt { get; set; }
    }
}
