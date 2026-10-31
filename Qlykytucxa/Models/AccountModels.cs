using System;
using System.ComponentModel.DataAnnotations;

namespace Qlykytucxa.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tên tài khoản hoặc mã sinh viên!")]
        [Display(Name = "Tài khoản")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu!")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu")]
        public string Password { get; set; }

        [Display(Name = "Ghi nhớ đăng nhập")]
        public bool RememberMe { get; set; }
    }

    public class UserSession
    {
        public string Username { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; } // "Admin", "Staff", "Student"
        public int? StudentId { get; set; }
        public int? RoomId { get; set; }
        public string RoomNumber { get; set; }
    }
}
