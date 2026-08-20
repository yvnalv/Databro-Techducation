using DataBro.Modules.Learning.Application;
using DataBro.Modules.Learning.Domain;
using Microsoft.EntityFrameworkCore;

namespace DataBro.Modules.Learning.Infrastructure.Persistence;

internal sealed class LearnerStreakRepository(LearningDbContext db) : ILearnerStreakRepository
{
    public Task<LearnerStreak?> GetAsync(Guid userId, CancellationToken ct = default)
        => db.LearnerStreaks.FirstOrDefaultAsync(s => s.UserId == userId, ct);

    public async Task AddAsync(LearnerStreak streak, CancellationToken ct = default)
        => await db.LearnerStreaks.AddAsync(streak, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
