using System;
using System.ComponentModel.DataAnnotations;

namespace Qlykytucxa.Models
{
    public class Announcement
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Tiêu đề thông báo không được để trống!")]
        [Display(Name = "Tiêu đề")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn danh mục thông báo!")]
        [Display(Name = "Danh mục")]
        public string Category { get; set; } // "Nội quy KTX", "Điện nước", "Bảo trì", "Vệ sinh", "Thông báo chung"

        [Required(ErrorMessage = "Nội dung thông báo không được để trống!")]
        [Display(Name = "Nội dung")]
        [DataType(DataType.MultilineText)]
        public string Content { get; set; }

        [Display(Name = "Mức độ ưu tiên")]
        public string Priority { get; set; } // "Khẩn cấp", "Quan trọng", "Thông thường"

        [Display(Name = "Ngày đăng")]
        public DateTime CreatedAt { get; set; }

        [Display(Name = "Người đăng")]
        public string AuthorName { get; set; }

        [Display(Name = "Kích hoạt hiển thị")]
        public bool IsActive { get; set; }
    }
}
