using System;
using System.ComponentModel.DataAnnotations;
using System.Web;

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

    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại!")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu hiện tại")]
        public string CurrentPassword { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới!")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu mới phải có ít nhất 8 ký tự!")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu mới")]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu mới!")]
        [Compare("NewPassword", ErrorMessage = "Mật khẩu nhập lại không khớp!")]
        [DataType(DataType.Password)]
        [Display(Name = "Nhập lại mật khẩu mới")]
        public string ConfirmPassword { get; set; }
    }

    // Tai khoan dang nhap (bang dbo.UserAccounts). Mat khau chi luu dang bam PBKDF2.
    public class UserAccount
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public string Role { get; set; }
        public string FullName { get; set; }
        public int? StudentId { get; set; }          // Chi co voi tai khoan sinh vien
        public bool MustChangePassword { get; set; } // Mat khau mac dinh / vua bi dat lai -> bat doi khi dang nhap
        public int FailedLoginCount { get; set; }
        public DateTime? LockoutEnd { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // Ten cac vai tro trong he thong
    public static class UserRoles
    {
        public const string Admin = "Admin";
        public const string Staff = "Staff";
        public const string Student = "Student";
    }

    public class UserSession
    {
        public string Username { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; } // "Admin", "Staff", "Student"
        public int? StudentId { get; set; }
        public int? RoomId { get; set; }
        public string RoomNumber { get; set; }
        public int AccountId { get; set; }
        public bool MustChangePassword { get; set; }

        // Lay nguoi dung dang dang nhap (null neu chua dang nhap)
        public static UserSession FromSession(HttpSessionStateBase session)
        {
            return session == null ? null : session["User"] as UserSession;
        }

        // Trang mac dinh cua tung vai tro sau khi dang nhap / khi bi tu choi quyen
        public static object HomeRoute(string role)
        {
            switch (role)
            {
                case UserRoles.Student: return new { controller = "StudentPortal", action = "Index" };
                case UserRoles.Staff: return new { controller = "Incidents", action = "Index" };
                default: return new { controller = "Home", action = "Index" };
            }
        }
    }
}
