using System.Linq;
using Qlykytucxa.Models;

namespace Qlykytucxa.Services
{
    // Rooms.CurrentOccupancy chi la so lieu tong hop; nguon du lieu goc la cac hop dong "Đang ở".
    public static class OccupancyService
    {
        // Tinh lai so nguoi dang o cua moi phong. Goi SAU khi da SaveChanges thay doi hop dong.
        public static void Recalculate(MiniDormDBEntities db)
        {
            db.Database.ExecuteSqlCommand(@"
UPDATE r SET CurrentOccupancy = ISNULL(c.Cnt, 0)
FROM dbo.Rooms r
LEFT JOIN (SELECT RoomId, COUNT(*) AS Cnt FROM dbo.RoomRegistrations WHERE Status = {0} GROUP BY RoomId) c
       ON c.RoomId = r.RoomId
WHERE ISNULL(r.CurrentOccupancy, -1) <> ISNULL(c.Cnt, 0)", DormConstants.StatusDangO);
        }

        // So sinh vien dang o trong phong (co the bo qua 1 hop dong, dung khi sua hop dong do)
        public static int ActiveCount(MiniDormDBEntities db, int roomId, int? excludeRegistrationId = null)
        {
            return db.RoomRegistrations.Count(r => r.RoomId == roomId
                                                && r.Status == DormConstants.StatusDangO
                                                && (excludeRegistrationId == null || r.RegistrationId != excludeRegistrationId));
        }
    }
}
