using System;
using System.Configuration;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Core.EntityClient;

namespace Qlykytucxa.Models
{
    // Context code-first cho cac bang mo rong (truoc day luu bang file JSON trong App_Data).
    // Dung chung CSDL MiniDormDB voi MiniDormDBEntities (EDMX); bang duoc tao boi DatabaseSetup.
    public class DormExtContext : DbContext
    {
        static DormExtContext()
        {
            // Schema do DatabaseSetup quan ly, EF khong tu tao/kiem tra CSDL
            Database.SetInitializer<DormExtContext>(null);
        }

        public DormExtContext()
            : base(GetConnectionString())
        {
        }

        private DormExtContext(DbConnection connection)
            : base(connection, false)
        {
        }

        // Dung chung ket noi voi context EDMX de ghi ca hai trong cung mot transaction:
        //   using (var tx = db.Database.BeginTransaction())
        //   using (var ext = DormExtContext.ShareConnection(db)) { ext.Database.UseTransaction(tx.UnderlyingTransaction); ... }
        public static DormExtContext ShareConnection(MiniDormDBEntities db)
        {
            return new DormExtContext(db.Database.Connection);
        }

        // Lay chuoi ket noi SQL thuan tu chuoi ket noi EntityClient cua EDMX (chi khai bao 1 noi trong Web.config)
        public static string GetConnectionString()
        {
            string entityCs = ConfigurationManager.ConnectionStrings["MiniDormDBEntities"].ConnectionString;
            return new EntityConnectionStringBuilder(entityCs).ProviderConnectionString;
        }

        public DbSet<Announcement> Announcements { get; set; }
        public DbSet<IncidentRequest> IncidentRequests { get; set; }
        public DbSet<CashFlowTransaction> CashFlowTransactions { get; set; }
        public DbSet<RoomFeePayment> RoomFeePayments { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Properties<DateTime>().Configure(c => c.HasColumnType("datetime2"));
            modelBuilder.Properties<decimal>().Configure(c => c.HasPrecision(18, 0));

            modelBuilder.Entity<Announcement>().ToTable("Announcements");
            modelBuilder.Entity<Announcement>().Property(a => a.Title).HasMaxLength(300);
            modelBuilder.Entity<Announcement>().Property(a => a.Category).HasMaxLength(100);
            modelBuilder.Entity<Announcement>().Property(a => a.Priority).HasMaxLength(50);
            modelBuilder.Entity<Announcement>().Property(a => a.AuthorName).HasMaxLength(200);

            modelBuilder.Entity<IncidentRequest>().ToTable("IncidentRequests");
            modelBuilder.Entity<IncidentRequest>().Property(i => i.RoomNumber).HasMaxLength(20);
            modelBuilder.Entity<IncidentRequest>().Property(i => i.Title).HasMaxLength(300);
            modelBuilder.Entity<IncidentRequest>().Property(i => i.Category).HasMaxLength(50);
            modelBuilder.Entity<IncidentRequest>().Property(i => i.ReporterName).HasMaxLength(100);
            modelBuilder.Entity<IncidentRequest>().Property(i => i.AssignedTo).HasMaxLength(100);
            modelBuilder.Entity<IncidentRequest>().Property(i => i.Status).HasMaxLength(30);

            modelBuilder.Entity<CashFlowTransaction>().ToTable("CashFlowTransactions");
            modelBuilder.Entity<CashFlowTransaction>().Property(t => t.Type).HasMaxLength(10).IsRequired();
            modelBuilder.Entity<CashFlowTransaction>().Property(t => t.Category).HasMaxLength(100).IsRequired();
            modelBuilder.Entity<CashFlowTransaction>().Property(t => t.RelatedPerson).HasMaxLength(200);
            modelBuilder.Entity<CashFlowTransaction>().Property(t => t.RoomNumber).HasMaxLength(50);
            modelBuilder.Entity<CashFlowTransaction>().Property(t => t.PaymentMethod).HasMaxLength(50);

            modelBuilder.Entity<RoomFeePayment>().ToTable("RoomFeePayments");
            modelBuilder.Entity<RoomFeePayment>().Property(f => f.StudentCode).HasMaxLength(20);
            modelBuilder.Entity<RoomFeePayment>().Property(f => f.StudentName).HasMaxLength(100);
            modelBuilder.Entity<RoomFeePayment>().Property(f => f.RoomNumber).HasMaxLength(20);
            modelBuilder.Entity<RoomFeePayment>().Property(f => f.SemesterPeriod).HasMaxLength(50);
            modelBuilder.Entity<RoomFeePayment>().Property(f => f.PaymentMethod).HasMaxLength(50);
            modelBuilder.Entity<RoomFeePayment>().Property(f => f.Note).HasMaxLength(500);
        }
    }
}
