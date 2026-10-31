using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using Qlykytucxa.Filters;
using Qlykytucxa.Models;

namespace Qlykytucxa.Controllers
{
    [RoleAuthorize(UserRoles.Admin)]
    public class HomeController : Controller
    {
        private MiniDormDBEntities db = new MiniDormDBEntities();
        private DormExtContext ext = new DormExtContext();

        public ActionResult Index()
        {
            var allRooms = db.Rooms.ToList();

            // 1. Thống kê số lượng tổng quan
            ViewBag.TotalStudents = db.Students.Count();
            ViewBag.TotalRooms = allRooms.Count;
            ViewBag.ActiveContracts = db.RoomRegistrations.Count(r => r.Status == DormConstants.StatusDangO);
            ViewBag.UnpaidBills = db.UtilityBills.Count(b => !b.IsPaid);
            ViewBag.UnpaidAmount = db.UtilityBills.Where(b => !b.IsPaid).Sum(b => (decimal?)b.TotalAmount) ?? 0;

            // 2. Tỷ lệ lấp đầy phòng thực tế
            int totalOccupancy = allRooms.Sum(r => r.CurrentOccupancy ?? 0);
            int totalCapacity = allRooms.Sum(r => r.MaxCapacity);
            int availableSlots = Math.Max(0, totalCapacity - totalOccupancy);
            int occupancyRate = totalCapacity > 0 ? (int)Math.Round((double)totalOccupancy * 100 / totalCapacity) : 0;

            ViewBag.TotalOccupancy = totalOccupancy;
            ViewBag.TotalCapacity = totalCapacity;
            ViewBag.AvailableSlots = availableSlots;
            ViewBag.OccupancyRate = occupancyRate;

            // Phân loại phòng theo tình trạng
            ViewBag.EmptyRooms = allRooms.Count(r => (r.CurrentOccupancy ?? 0) == 0);
            ViewBag.FullRooms = allRooms.Count(r => (r.CurrentOccupancy ?? 0) >= r.MaxCapacity);
            ViewBag.PartialRooms = allRooms.Count(r => (r.CurrentOccupancy ?? 0) > 0 && (r.CurrentOccupancy ?? 0) < r.MaxCapacity);

            // 3. Biểu đồ tỷ lệ sử dụng phòng theo từng tòa (Building-level occupancy, trích xuất từ tiền tố phòng)
            var buildingGroups = allRooms
                .GroupBy(r => (!string.IsNullOrEmpty(r.RoomNumber) && char.IsLetter(r.RoomNumber[0])) 
                              ? ("Tòa " + r.RoomNumber[0]) 
                              : "Khu KTX")
                .Select(g => new
                {
                    Building = g.Key,
                    Occupied = g.Sum(r => r.CurrentOccupancy ?? 0),
                    Capacity = g.Sum(r => r.MaxCapacity)
                })
                .OrderBy(x => x.Building)
                .ToList();

            ViewBag.BuildingLabels = buildingGroups.Select(b => b.Building).ToArray();
            ViewBag.BuildingOccupied = buildingGroups.Select(b => b.Occupied).ToArray();
            ViewBag.BuildingCapacity = buildingGroups.Select(b => b.Capacity).ToArray();

            // 4. Biểu đồ Doanh thu và Công nợ trong 6 tháng gần nhất
            var allBills = db.UtilityBills.ToList();
            var recentMonths = allBills
                .GroupBy(b => new { b.BillingYear, b.BillingMonth })
                .Select(g => new
                {
                    Year = g.Key.BillingYear,
                    Month = g.Key.BillingMonth,
                    Revenue = g.Where(x => x.IsPaid).Sum(x => x.TotalAmount),
                    Debt = g.Where(x => !x.IsPaid).Sum(x => x.TotalAmount)
                })
                .OrderByDescending(x => x.Year).ThenByDescending(x => x.Month)
                .Take(6)
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToList();

            ViewBag.FinanceLabels = recentMonths.Select(x => $"T{x.Month}/{x.Year}").ToArray();
            ViewBag.FinanceRevenue = recentMonths.Select(x => x.Revenue).ToArray();
            ViewBag.FinanceDebt = recentMonths.Select(x => x.Debt).ToArray();

            // 5. Danh sách hóa đơn chưa thanh toán (Top 5)
            var pendingBills = db.UtilityBills
                                .Include(b => b.Rooms)
                                .Where(b => !b.IsPaid)
                                .OrderByDescending(b => b.BillingYear)
                                .ThenByDescending(b => b.BillingMonth)
                                .Take(5)
                                .ToList();
            ViewBag.PendingBills = pendingBills;

            // 6. Danh sách hợp đồng sắp hết hạn (trong vòng 30 ngày)
            DateTime in30Days = DateTime.Now.AddDays(30);
            var expiringContracts = db.RoomRegistrations
                                      .Include(r => r.Students)
                                      .Include(r => r.Rooms)
                                      .Where(r => r.Status == DormConstants.StatusDangO
                                               && r.CheckOutDate.HasValue 
                                               && r.CheckOutDate.Value <= in30Days)
                                      .OrderBy(r => r.CheckOutDate.Value)
                                      .Take(5)
                                      .ToList();
            ViewBag.ExpiringContracts = expiringContracts;

            // 7. Danh sách yêu cầu sửa chữa chưa xử lý (từ Incidents)
            ViewBag.PendingIncidents = ext.IncidentRequests
                .Where(i => i.Status != DormConstants.IncidentHoanThanh)
                .OrderByDescending(i => i.CreatedAt)
                .Take(5)
                .ToList();

            // 8. Bảng tin thông báo KTX mới nhất
            ViewBag.Announcements = ext.Announcements
                .Where(a => a.IsActive)
                .OrderByDescending(a => a.CreatedAt)
                .Take(3)
                .ToList();

            return View();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
                ext.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
