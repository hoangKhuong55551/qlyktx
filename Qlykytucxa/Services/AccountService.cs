using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Qlykytucxa.Models;

namespace Qlykytucxa.Services
{
    public static class AccountService
    {
        // Mat khau mac dinh cho tai khoan moi tao; bat buoc doi o lan dang nhap dau tien
        public const string DefaultPassword = "123456";

        public const int MaxFailedLogins = 5;
        public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int Iterations = 100000;

        #region Bam mat khau (PBKDF2-SHA256)
        // Dinh dang luu: PBKDF2$<so vong lap>$<salt base64>$<hash base64>
        public static string HashPassword(string password)
        {
            byte[] salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);

            byte[] hash = Pbkdf2(password, salt, Iterations);
            return $"PBKDF2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        public static bool VerifyPassword(string password, string stored)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(stored)) return false;
            string[] parts = stored.Split('$');
            if (parts.Length != 4 || parts[0] != "PBKDF2") return false;

            int iterations;
            if (!int.TryParse(parts[1], out iterations) || iterations <= 0) return false;
            try
            {
                byte[] salt = Convert.FromBase64String(parts[2]);
                byte[] expected = Convert.FromBase64String(parts[3]);
                byte[] actual = Pbkdf2(password, salt, iterations, expected.Length);
                return FixedTimeEquals(actual, expected);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static byte[] Pbkdf2(string password, byte[] salt, int iterations, int size = HashSize)
        {
            using (var kdf = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
            {
                return kdf.GetBytes(size);
            }
        }

        // So sanh khong phu thuoc thoi gian (tranh do mat khau qua thoi gian phan hoi)
        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        // Mat khau tam thoi khi Admin dat lai (bo ky tu de nham: 0/O, 1/l/I)
        public static string GenerateTemporaryPassword(int length = 10)
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
            byte[] bytes = new byte[length];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
        }
        #endregion

        #region Dong bo tai khoan
        // Tao tai khoan admin/staff mac dinh (neu chua co) va tai khoan cho moi sinh vien chua co tai khoan.
        // Tai khoan sinh vien: ten dang nhap = ma SV. Ma SV doi -> doi ten dang nhap theo.
        public static void SyncAccounts(MiniDormDBEntities db, DormExtContext ext)
        {
            var accounts = ext.UserAccounts.ToList();
            bool changed = false;

            // Bam 1 lan roi dung chung cho cac tai khoan moi (deu la mat khau mac dinh, bat buoc doi) -> khoi dong nhanh
            string defaultHash = null;
            Func<string> hash = () => defaultHash ?? (defaultHash = HashPassword(DefaultPassword));

            if (!accounts.Any(a => a.Role == UserRoles.Admin))
            {
                ext.UserAccounts.Add(NewAccount("admin", UserRoles.Admin, "Thầy Trưởng Ban Quản lý KTX", null, hash()));
                changed = true;
            }
            // Chi tao staff mac dinh o lan cai dat dau tien (chua co tai khoan quan tri nao).
            // Neu Admin da chu dong xoa het Staff thi khong tu tao lai tai khoan mat khau mac dinh.
            if (!accounts.Any(a => a.Role != UserRoles.Student))
            {
                ext.UserAccounts.Add(NewAccount("staff", UserRoles.Staff, "Nguyễn Văn Hùng (Kỹ thuật viên KTX)", null, hash()));
                changed = true;
            }

            var byStudent = accounts.Where(a => a.StudentId.HasValue).ToDictionary(a => a.StudentId.Value);
            var usedNames = new HashSet<string>(accounts.Select(a => a.Username), StringComparer.OrdinalIgnoreCase);

            foreach (var s in db.Students.ToList())
            {
                if (string.IsNullOrWhiteSpace(s.StudentCode)) continue;
                string code = s.StudentCode.Trim();

                UserAccount acc;
                if (byStudent.TryGetValue(s.StudentId, out acc))
                {
                    if (!string.Equals(acc.Username, code, StringComparison.OrdinalIgnoreCase) && !usedNames.Contains(code))
                    {
                        usedNames.Remove(acc.Username);
                        acc.Username = code;
                        usedNames.Add(code);
                        changed = true;
                    }
                    if (acc.FullName != s.FullName)
                    {
                        acc.FullName = s.FullName;
                        changed = true;
                    }
                }
                else if (usedNames.Add(code))
                {
                    ext.UserAccounts.Add(NewAccount(code, UserRoles.Student, s.FullName, s.StudentId, hash()));
                    changed = true;
                }
            }

            if (changed) ext.SaveChanges();
        }

        private static UserAccount NewAccount(string username, string role, string fullName, int? studentId, string passwordHash)
        {
            return new UserAccount
            {
                Username = username,
                PasswordHash = passwordHash,
                Role = role,
                FullName = fullName,
                StudentId = studentId,
                MustChangePassword = true,
                CreatedAt = DateTime.Now
            };
        }
        #endregion
    }
}
