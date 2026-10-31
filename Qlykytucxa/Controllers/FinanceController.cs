using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using Newtonsoft.Json;
using Qlykytucxa.Filters;
using Qlykytucxa.Models;

namespace Qlykytucxa.Controllers
{
    [RoleAuthorize(UserRoles.Admin)]
    public class FinanceController : Controller
    {
        private MiniDormDBEntities db = new MiniDormDBEntities();
        private static readonly object _fileLock = new object();

        #region Storage Helpers (JSON backed in App_Data)
        private string GetCashFlowPath()
        {
            string appData = Server.MapPath("~/App_Data");
            if (!Directory.Exists(appData)) Directory.CreateDirectory(appData);
            return Path.Combine(appData, "cashflow.json");
        }

        private string GetRoomFeesPath()
        {
            string appData = Server.MapPath("~/App_Data");
            if (!Directory.Exists(appData)) Directory.CreateDirectory(appData);
            return Path.Combine(appData, "room_fees.json");
        }

        private List<CashFlowTransaction> LoadTransactions()
        {
            lock (_fileLock)
            {
                string path = GetCashFlowPath();
                if (!System.IO.File.Exists(path))
                {
                    // Dữ liệu mẫu ban đầu
                    var defaults = new List<CashFlowTransaction>
                    {
                        new CashFlowTransaction
                        {
                            Id = 1,
                            TransactionDate = DateTime.Now.AddDays(-20),
                            Type = "Thu",
                            Category = "Tiền phòng",
                            Amount = 1500000,
                            RelatedPerson = "Nguyễn Văn An (SV001)",
                            RoomNumber = "A101",
                            Description = "Thu tiền phòng học kỳ 1 (Phòng A101)",
                            PaymentMethod = "Chuyển khoản VietQR"
                        },
                        new CashFlowTransaction
                        {
                            Id = 2,
                            TransactionDate = DateTime.Now.AddDays(-18),
                            Type = "Thu",
                            Category = "Tiền phòng",
                            Amount = 1500000,
                            RelatedPerson = "Trần Quốc Bảo (SV002)",
                            RoomNumber = "A101",
                            Description = "Thu tiền phòng học kỳ 1 (Phòng A101)",
                            PaymentMethod = "Tiền mặt"
                        },
                        new CashFlowTransaction
                        {
                            Id = 3,
                            TransactionDate = DateTime.Now.AddDays(-10),
                            Type = "Chi",
                            Category = "Chi bảo trì",
                            Amount = 350000,
                            RelatedPerson = "Chú Hùng (Thợ điện nước)",
                            RoomNumber = "A102",
                            Description = "Chi phí thay van vòi nước và dây cấp nước phòng A102",
                            PaymentMethod = "Tiền mặt"
                        },
                        new CashFlowTransaction
                        {
                            Id = 4,
                            TransactionDate = DateTime.Now.AddDays(-5),
                            Type = "Chi",
                            Category = "Chi mua sắm vật tư",
                            Amount = 850000,
                            RelatedPerson = "Cửa hàng Điện tử Dân sinh",
                            RoomNumber = "Tòa A",
                            Description = "Mua 10 bóng đèn tuýp LED và 5 atomat thay thế định kỳ",
                            PaymentMethod = "Chuyển khoản"
                        }
                    };
                    SaveTransactions(defaults);
                    return defaults;
                }
                string json = System.IO.File.ReadAllText(path);
                return JsonConvert.DeserializeObject<List<CashFlowTransaction>>(json) ?? new List<CashFlowTransaction>();
            }
        }

        private void SaveTransactions(List<CashFlowTransaction> list)
        {
            lock (_fileLock)
            {
                string path = GetCashFlowPath();
                string json = JsonConvert.SerializeObject(list, Formatting.Indented);
                System.IO.File.WriteAllText(path, json);
            }
        }

        private List<RoomFeePayment> LoadRoomFees()
        {
            lock (_fileLock)
            {
                string path = GetRoomFeesPath();
                List<RoomFeePayment> list = new List<RoomFeePayment>();
                if (System.IO.File.Exists(path))
                {
                    string json = System.IO.File.ReadAllText(path);
                    list = JsonConvert.DeserializeObject<List<RoomFeePayment>>(json) ?? new List<RoomFeePayment>();
                }

                // Tự động đồng bộ các sinh viên đang ở trong database chưa có trong danh sách thu tiền phòng
                var activeRegs = db.RoomRegistrations.Include(r => r.Students).Include(r => r.Rooms)
                    .Where(r => r.Status == "Đang ở" || r.Status == DormConstants.StatusDangO)
                    .ToList();

                bool needSave = false;
                foreach (var reg in activeRegs)
                {
                    if (!list.Any(f => f.RegistrationId == reg.RegistrationId))
                    {
                        int nextId = list.Any() ? list.Max(x => x.Id) + 1 : 1;
                        list.Add(new RoomFeePayment
                        {
                            Id = nextId,
                            RegistrationId = reg.RegistrationId,
                            StudentId = reg.StudentId,
                            StudentCode = reg.Students.StudentCode,
                            StudentName = reg.Students.FullName,
                            RoomId = reg.RoomId,
                            RoomNumber = reg.Rooms.RoomNumber,
                            Amount = reg.Rooms.RoomPrice,
                            SemesterPeriod = $"Kỳ {DateTime.Now.Month / 6 + 1} ({DateTime.Now.Year})",
                            DueDate = reg.CheckInDate.AddDays(15),
                            PaidDate = null,
                            PaymentMethod = null,
                            IsPaid = false,
                            Note = "Thu tiền phòng định kỳ"
                        });
                        needSave = true;
                    }
                }

                if (needSave)
                {
                    SaveRoomFees(list);
                }
                return list;
            }
        }

        private void SaveRoomFees(List<RoomFeePayment> list)
        {
            lock (_fileLock)
            {
                string path = GetRoomFeesPath();
                string json = JsonConvert.SerializeObject(list, Formatting.Indented);
                System.IO.File.WriteAllText(path, json);
            }
        }
        #endregion

        // GET: Finance (Báo cáo Thu - Chi tổng thể KTX)
        public ActionResult Index(string typeFilter, int? monthFilter, int? yearFilter)
        {
            var transactions = LoadTransactions();
            var roomFees = LoadRoomFees();

            // 1. Thống kê Doanh thu
            decimal paidRoomFees = roomFees.Where(f => f.IsPaid).Sum(f => f.Amount);
            decimal paidUtilities = db.UtilityBills.Where(b => b.IsPaid).Sum(b => (decimal?)b.TotalAmount) ?? 0;
            decimal otherRevenue = transactions.Where(t => t.Type == "Thu" && t.Category != "Tiền phòng" && t.Category != "Tiền điện nước").Sum(t => t.Amount);

            decimal totalRevenue = paidRoomFees + paidUtilities + otherRevenue;

            // 2. Thống kê Chi phí
            decimal totalExpense = transactions.Where(t => t.Type == "Chi").Sum(t => t.Amount);

            // 3. Lợi nhuận ròng
            decimal netProfit = totalRevenue - totalExpense;

            // 4. Công nợ cần thu
            decimal unpaidRoomFees = roomFees.Where(f => !f.IsPaid).Sum(f => f.Amount);
            decimal unpaidUtilities = db.UtilityBills.Where(b => !b.IsPaid).Sum(b => (decimal?)b.TotalAmount) ?? 0;
            decimal totalReceivable = unpaidRoomFees + unpaidUtilities;

            // 5. Chuẩn bị dữ liệu biểu đồ Thu - Chi qua các tháng gần đây
            var chartLabels = new List<string>();
            var chartRevenue = new List<decimal>();
            var chartExpense = new List<decimal>();
            var chartProfit = new List<decimal>();

            for (int i = 5; i >= 0; i--)
            {
                var dt = DateTime.Now.AddMonths(-i);
                chartLabels.Add($"T{dt.Month}/{dt.Year}");

                // Thu trong tháng dt
                decimal mRev = transactions.Where(t => t.Type == "Thu" && t.TransactionDate.Month == dt.Month && t.TransactionDate.Year == dt.Year).Sum(t => t.Amount);
                // Chi trong tháng dt
                decimal mExp = transactions.Where(t => t.Type == "Chi" && t.TransactionDate.Month == dt.Month && t.TransactionDate.Year == dt.Year).Sum(t => t.Amount);

                chartRevenue.Add(mRev);
                chartExpense.Add(mExp);
                chartProfit.Add(mRev - mExp);
            }

            ViewBag.ChartLabels = chartLabels.ToArray();
            ViewBag.ChartRevenue = chartRevenue.ToArray();
            ViewBag.ChartExpense = chartExpense.ToArray();
            ViewBag.ChartProfit = chartProfit.ToArray();

            // 6. Lọc danh sách giao dịch
            var filteredTx = transactions.AsQueryable();
            if (!string.IsNullOrEmpty(typeFilter))
            {
                filteredTx = filteredTx.Where(t => t.Type == typeFilter);
            }
            if (monthFilter.HasValue && monthFilter.Value > 0)
            {
                filteredTx = filteredTx.Where(t => t.TransactionDate.Month == monthFilter.Value);
            }
            if (yearFilter.HasValue && yearFilter.Value > 0)
            {
                filteredTx = filteredTx.Where(t => t.TransactionDate.Year == yearFilter.Value);
            }

            ViewBag.TypeFilter = typeFilter;
            ViewBag.MonthFilter = monthFilter;
            ViewBag.YearFilter = yearFilter;

            var model = new FinanceReportViewModel
            {
                TotalRevenue = totalRevenue,
                TotalExpense = totalExpense,
                NetProfit = netProfit,
                TotalReceivable = totalReceivable,
                RoomFeeRevenue = paidRoomFees,
                UtilityRevenue = paidUtilities,
                RecentTransactions = filteredTx.OrderByDescending(t => t.TransactionDate).Take(15).ToList(),
                PendingRoomFees = roomFees.Where(f => !f.IsPaid).Take(5).ToList()
            };

            return View(model);
        }

        // GET: Finance/RoomFees (Quản lý Thu Tiền Phòng KTX)
        public ActionResult RoomFees(string searchString, string statusFilter)
        {
            var list = LoadRoomFees().AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                list = list.Where(f => f.StudentCode.Contains(searchString) 
                                    || f.StudentName.Contains(searchString) 
                                    || f.RoomNumber.Contains(searchString));
            }

            if (statusFilter == "paid")
            {
                list = list.Where(f => f.IsPaid);
            }
            else if (statusFilter == "unpaid")
            {
                list = list.Where(f => !f.IsPaid);
            }

            ViewBag.SearchString = searchString;
            ViewBag.StatusFilter = statusFilter;

            return View(list.OrderBy(f => f.IsPaid).ThenByDescending(f => f.DueDate).ToList());
        }

        // POST: Finance/CollectRoomFee (Thu tiền phòng cho sinh viên)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CollectRoomFee(int id, string paymentMethod = "Tiền mặt")
        {
            var fees = LoadRoomFees();
            var fee = fees.FirstOrDefault(f => f.Id == id);
            if (fee != null && !fee.IsPaid)
            {
                fee.IsPaid = true;
                fee.PaidDate = DateTime.Now;
                fee.PaymentMethod = paymentMethod;
                SaveRoomFees(fees);

                // Ghi tự động 1 bút toán vào Sổ quỹ Thu
                var txs = LoadTransactions();
                int nextTxId = txs.Any() ? txs.Max(x => x.Id) + 1 : 1;
                txs.Insert(0, new CashFlowTransaction
                {
                    Id = nextTxId,
                    TransactionDate = DateTime.Now,
                    Type = "Thu",
                    Category = "Tiền phòng",
                    Amount = fee.Amount,
                    RelatedPerson = $"{fee.StudentName} ({fee.StudentCode})",
                    RoomNumber = fee.RoomNumber,
                    Description = $"Thu tiền phòng {fee.SemesterPeriod} của sinh viên {fee.StudentName}",
                    PaymentMethod = paymentMethod
                });
                SaveTransactions(txs);

                TempData["Success"] = $"Đã xác nhận thu tiền phòng {fee.Amount:N0} đ từ sinh viên {fee.StudentName} (Phòng {fee.RoomNumber}) thành công!";
            }
            return RedirectToAction("RoomFees");
        }

        // POST: Finance/AddExpense (Ghi nhận một khoản chi phí vận hành KTX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddExpense(string category, decimal amount, string relatedPerson, string description, string paymentMethod = "Tiền mặt")
        {
            if (amount <= 0)
            {
                TempData["Error"] = "Số tiền chi phải lớn hơn 0!";
                return RedirectToAction("Index");
            }

            var txs = LoadTransactions();
            int nextId = txs.Any() ? txs.Max(x => x.Id) + 1 : 1;

            txs.Insert(0, new CashFlowTransaction
            {
                Id = nextId,
                TransactionDate = DateTime.Now,
                Type = "Chi",
                Category = string.IsNullOrEmpty(category) ? "Chi phí vận hành" : category,
                Amount = amount,
                RelatedPerson = relatedPerson ?? "Nhân viên phụ trách",
                RoomNumber = "Toàn KTX",
                Description = description ?? "Khoản chi bảo trì, sửa chữa, vận hành ký túc xá",
                PaymentMethod = paymentMethod
            });
            SaveTransactions(txs);

            TempData["Success"] = $"Đã ghi nhận khoản chi {amount:N0} đ ({category}) vào sổ quỹ!";
            return RedirectToAction("Index");
        }

        // GET: Finance/PrintRoomReceipt/5 (Mẫu in Phiếu thu tiền phòng KTX chuẩn A5 & VietQR)
        public ActionResult PrintRoomReceipt(int id)
        {
            var fee = LoadRoomFees().FirstOrDefault(f => f.Id == id);
            if (fee == null) return HttpNotFound();

            return View(fee);
        }

        // GET: Finance/ExportFinanceExcel (Xuất sổ quỹ thu chi ra file Excel CSV UTF-8)
        public ActionResult ExportFinanceExcel(string typeFilter, int? monthFilter, int? yearFilter)
        {
            var list = LoadTransactions().AsQueryable();
            if (!string.IsNullOrEmpty(typeFilter))
            {
                list = list.Where(t => t.Type == typeFilter);
            }
            if (monthFilter.HasValue && monthFilter.Value > 0)
            {
                list = list.Where(t => t.TransactionDate.Month == monthFilter.Value);
            }
            if (yearFilter.HasValue && yearFilter.Value > 0)
            {
                list = list.Where(t => t.TransactionDate.Year == yearFilter.Value);
            }

            var items = list.OrderByDescending(t => t.TransactionDate).ToList();

            var sb = new StringBuilder();
            sb.Append('\uFEFF'); // UTF-8 BOM
            sb.AppendLine("Mã GD,Ngày giao dịch,Loại (Thu/Chi),Khoản mục,Số tiền (VNĐ),Người nộp/Người nhận,Phòng liên quan,Hình thức,Nội dung chi tiết");

            foreach (var t in items)
            {
                sb.AppendLine($"GD{t.Id:D4},{t.TransactionDate:dd/MM/yyyy HH:mm},{t.Type},{t.Category},{t.Amount},{t.RelatedPerson},{t.RoomNumber},{t.PaymentMethod},\"{t.Description}\"");
            }

            byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
            string fileName = $"SoQuy_ThuChi_KTX_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            return File(bytes, "text/csv; charset=utf-8", fileName);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
