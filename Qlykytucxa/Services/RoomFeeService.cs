using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using Qlykytucxa.Models;

namespace Qlykytucxa.Services
{
    // Sinh khoan tien phong hang thang cho sinh vien dang o (gia phong tinh theo thang / sinh vien).
    public static class RoomFeeService
    {
        // Tao cac khoan tien phong con thieu, tu thang vao o den thang hien tai, cho moi hop dong dang o.
        // Moi sinh vien chi bi tinh 1 lan / thang (thang chuyen phong khong bi tinh trung).
        // Can goi truoc khi ket thuc hop dong (tra phong, thanh ly, chuyen phong) de khong bo sot thang cuoi.
        public static int EnsureMonthlyFees(MiniDormDBEntities db, DormExtContext ext)
        {
            DateTime today = DateTime.Today;
            var activeRegs = db.RoomRegistrations.Include(r => r.Students).Include(r => r.Rooms)
                .Where(r => r.Status == DormConstants.StatusDangO)
                .ToList();

            var existing = new HashSet<string>(
                ext.RoomFeePayments.Select(f => new { f.StudentId, f.FeeYear, f.FeeMonth }).ToList()
                   .Select(k => Key(k.StudentId, k.FeeYear, k.FeeMonth)));

            int added = 0;
            foreach (var reg in activeRegs)
            {
                var firstMonth = new DateTime(reg.CheckInDate.Year, reg.CheckInDate.Month, 1);
                for (var month = firstMonth; month <= today; month = month.AddMonths(1))
                {
                    if (!existing.Add(Key(reg.StudentId, month.Year, month.Month))) continue;

                    ext.RoomFeePayments.Add(new RoomFeePayment
                    {
                        RegistrationId = reg.RegistrationId,
                        StudentId = reg.StudentId,
                        StudentCode = reg.Students.StudentCode,
                        StudentName = reg.Students.FullName,
                        RoomId = reg.RoomId,
                        RoomNumber = reg.Rooms.RoomNumber,
                        FeeMonth = month.Month,
                        FeeYear = month.Year,
                        Amount = reg.Rooms.RoomPrice,
                        SemesterPeriod = $"Tháng {month.Month:D2}/{month.Year}",
                        // Thang dau: han 15 ngay sau khi vao o; cac thang sau: ngay 15 hang thang
                        DueDate = month == firstMonth ? reg.CheckInDate.AddDays(15) : month.AddDays(14),
                        IsPaid = false,
                        Note = "Thu tiền phòng hàng tháng"
                    });
                    added++;
                }
            }

            if (added > 0)
            {
                try
                {
                    ext.SaveChanges();
                }
                catch (DbUpdateException)
                {
                    // Request khac vua sinh cung khoan phi (vi pham UNIQUE) -> bo qua, lan sau se dong bo lai
                    foreach (var entry in ext.ChangeTracker.Entries<RoomFeePayment>().Where(e => e.State == EntityState.Added).ToList())
                    {
                        entry.State = EntityState.Detached;
                    }
                    added = 0;
                }
            }
            return added;
        }

        private static string Key(int studentId, int year, int month)
        {
            return studentId + "-" + year + "-" + month;
        }
    }
}
