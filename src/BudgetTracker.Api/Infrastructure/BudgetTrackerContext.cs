using BudgetTracker.Api.Auth;
using BudgetTracker.Api.Features.Transactions;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Infrastructure;

public class BudgetTrackerContext : IdentityDbContext<ApplicationUser>
{
	public BudgetTrackerContext(DbContextOptions<BudgetTrackerContext> options) : base(options)
	{
	}

	public DbSet<Transaction> Transactions { get; set; }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		modelBuilder.Entity<Transaction>(entity =>
		{
			entity.HasKey(e => e.Id);

			entity.Property(e => e.Id)
				.HasDefaultValueSql("gen_random_uuid()");

			entity.HasOne<ApplicationUser>()
				.WithMany()
				.HasForeignKey(t => t.UserId)
				.HasPrincipalKey(u => u.Id)
				.OnDelete(DeleteBehavior.Cascade);

			entity.HasIndex(t => t.Date);
			entity.HasIndex(t => t.UserId);
			entity.HasIndex(t => t.ImportedAt);
		});
	}
}
