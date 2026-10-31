using System;
using System.ComponentModel.DataAnnotations;

namespace Qlykytucxa.Models
{
    // ============================================================
    // Hang so dung chung toan ung dung
    // ============================================================
    public static class DormConstants
    {
        // Trang thai xep phong - phai khop chinh xac voi du lieu trong database
        public const string StatusDangO = "Đang ở";
        public const string StatusDaTraPhong = "Đã trả phòng";
        public const string StatusDaThanhLy = "Đã thanh lý";

        // Trang thai yeu cau su co
        public const string IncidentMoiTiepNhan = "Mới tiếp nhận";
        public const string IncidentDangXuLy = "Đang xử lý";
        public const string IncidentHoanThanh = "Đã hoàn thành";
        public const string ChuaPhanCong = "Chưa phân công";

        // Loai giao dich so quy va khoan muc thu tu dong
        public const string GiaoDichThu = "Thu";
        public const string GiaoDichChi = "Chi";
        public const string KhoanTienPhong = "Tiền phòng";
        public const string KhoanDienNuoc = "Tiền điện nước";

        // Don gia dien/nuoc (VND) - chinh tai day khi can thay doi
        public const decimal ElectricPrice = 3500m;   // d/kWh
        public const decimal WaterPrice = 15000m;     // d/m3
    }

    // 1. Viet hoa bang Phong
    [MetadataType(typeof(RoomMetadata))]
    public partial class Rooms { }
    public class RoomMetadata
    {
        [Display(Name = "Số phòng")]
        [Required(ErrorMessage = "Vui lòng nhập số phòng")]
        public string RoomNumber { get; set; }

        [Display(Name = "Loại phòng")]
        [Required(ErrorMessage = "Vui lòng nhập loại phòng")]
        public string RoomType { get; set; }

        [Display(Name = "Sức chứa tối đa")]
        [Range(1, 20, ErrorMessage = "Sức chứa từ 1 đến 20 người")]
        public int MaxCapacity { get; set; }

        [Display(Name = "Đang ở")]
        public Nullable<int> CurrentOccupancy { get; set; }

        [Display(Name = "Giá phòng (VNĐ)")]
        [DisplayFormat(DataFormatString = "{0:N0} đ")]
        public decimal RoomPrice { get; set; }
    }

    // 2. Viet hoa bang Sinh vien
    [MetadataType(typeof(StudentMetadata))]
    public partial class Students { }
    public class StudentMetadata
    {
        [Display(Name = "Mã SV")]
        [Required(ErrorMessage = "Vui lòng nhập mã sinh viên")]
        public string StudentCode { get; set; }

        [Display(Name = "Họ và tên")]
        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        public string FullName { get; set; }

        [Display(Name = "Giới tính")]
        public string Gender { get; set; }

        [Display(Name = "Số điện thoại")]
        public string Phone { get; set; }

        [Display(Name = "Lớp")]
        public string ClassName { get; set; }
    }

    // 3. Viet hoa bang Xep phong
    [MetadataType(typeof(RoomRegistrationMetadata))]
    public partial class RoomRegistrations { }
    public class RoomRegistrationMetadata
    {
        [Display(Name = "Sinh viên")]
        public int StudentId { get; set; }

        [Display(Name = "Phòng")]
        public int RoomId { get; set; }

        [Display(Name = "Ngày vào ở")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy}", ApplyFormatInEditMode = true)]
        public System.DateTime CheckInDate { get; set; }

        [Display(Name = "Ngày trả phòng")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy}", ApplyFormatInEditMode = true)]
        public Nullable<System.DateTime> CheckOutDate { get; set; }

        [Display(Name = "Trạng thái")]
        public string Status { get; set; }
    }

    // 4. Viet hoa bang Hoa don Dien Nuoc
    [MetadataType(typeof(UtilityBillMetadata))]
    public partial class UtilityBills { }
    public class UtilityBillMetadata
    {
        [Display(Name = "Phòng")]
        public int RoomId { get; set; }

        [Display(Name = "Tháng")]
        [Range(1, 12, ErrorMessage = "Tháng phải từ 1 đến 12")]
        public int BillingMonth { get; set; }

        [Display(Name = "Năm")]
        public int BillingYear { get; set; }

        [Display(Name = "Số điện cũ")]
        public int OldElectricIndex { get; set; }

        [Display(Name = "Số điện mới")]
        public int NewElectricIndex { get; set; }

        [Display(Name = "Số nước cũ")]
        public int OldWaterIndex { get; set; }

        [Display(Name = "Số nước mới")]
        public int NewWaterIndex { get; set; }

        [Display(Name = "Tổng tiền (VNĐ)")]
        [DisplayFormat(DataFormatString = "{0:N0} đ")]
        public decimal TotalAmount { get; set; }

        [Display(Name = "Đã thanh toán")]
        public bool IsPaid { get; set; }
    }
}
