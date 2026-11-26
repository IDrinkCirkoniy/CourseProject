using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connStr = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=rhythm_sort_db;Username=postgres;Password=your_password";

builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(connStr));

var app = builder.Build();
app.UseStaticFiles();

//Создание таблиц при первом запуске
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

//проверка пользователя и роли
static async Task<User?> ResolveUser(HttpRequest req, AppDbContext db, CancellationToken ct)
{
    var idHeader = req.Headers["X-User-Id"].FirstOrDefault();
    if (!Guid.TryParse(idHeader, out var userId))
        return null;

    return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
}

//Тестовый эндпоинт
app.MapPost("/api/users", async (CreateUserDto dto, AppDbContext db, CancellationToken ct) =>
{
    var role = dto.Role is Roles.Guest or Roles.Creator or Roles.Admin ? dto.Role : Roles.Guest;
    var user = new User
    {
        Username = dto.Username.Trim(),
        PasswordHash = dto.PasswordHash,
        Role = role
    };
    db.Users.Add(user);
    await db.SaveChangesAsync(ct);
    return Results.Created($"/api/users/{user.Id}", new { user.Id, user.Username, user.Role });
});

//Треки - просмотр
app.MapGet("/api/tracks", async (AppDbContext db, CancellationToken ct) =>
{
    var items = await db.Tracks
        .AsNoTracking()
        .OrderByDescending(t => t.CreatedAt)
        .ToListAsync(ct);
    return Results.Ok(new { items });
});

//Треки - загрузка
app.MapPost("/api/tracks", async (HttpRequest req, AppDbContext db, IWebHostEnvironment env, CancellationToken ct) =>
{
    var user = await ResolveUser(req, db, ct);
    if (user is null) return Results.Unauthorized();
    if (user.Role is not (Roles.Creator or Roles.Admin)) return Results.Forbid();

    var form = await req.ReadFormAsync(ct);
    var file = form.Files.GetFile("File");
    if (file is null || file.Length == 0)
        return Results.BadRequest("Аудиофайл не передан.");

    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (ext is not (".mp3" or ".ogg" or ".wav"))
        return Results.BadRequest("Поддерживаются только .mp3, .ogg и .wav.");

    var uploadDir = Path.Combine(env.WebRootPath ?? "wwwroot", "uploads", "tracks");
    Directory.CreateDirectory(uploadDir);

    var fileName = $"{Guid.NewGuid()}{ext}";
    await using var fs = File.Create(Path.Combine(uploadDir, fileName));
    await file.CopyToAsync(fs, ct);

    var track = new Track
    {
        Title = form["Title"].ToString().Trim(),
        Artist = form["Artist"].ToString().Trim(),
        Bpm = float.TryParse(form["Bpm"], out var bpm) ? bpm : 120f,
        DurationSeconds = float.TryParse(form["DurationSeconds"], out var dur) ? dur : 0f,
        AudioUrl = $"/uploads/tracks/{fileName}",
        UploadedByUserId = user.Id
    };

    db.Tracks.Add(track);
    await db.SaveChangesAsync(ct);
    return Results.Created($"/api/tracks/{track.Id}", track);
});

//Треки - редактирование
app.MapPut("/api/tracks/{id:guid}", async (Guid id, UpdateTrackDto dto, HttpRequest req, AppDbContext db, CancellationToken ct) =>
{
    var user = await ResolveUser(req, db, ct);
    if (user is null) return Results.Unauthorized();

    var track = await db.Tracks.FirstOrDefaultAsync(t => t.Id == id, ct);
    if (track is null) return Results.NotFound();

    bool canEdit = user.Role == Roles.Admin || (user.Role == Roles.Creator && track.UploadedByUserId == user.Id);
    if (!canEdit) return Results.Forbid();

    track.Title = dto.Title.Trim();
    track.Artist = dto.Artist.Trim();
    track.Bpm = dto.Bpm;

    await db.SaveChangesAsync(ct);
    return Results.NoContent();
});

//Карты ритма - просмотр
app.MapGet("/api/beatmaps", async (Guid? trackId, AppDbContext db, CancellationToken ct) =>
{
    var query = db.Beatmaps.AsNoTracking().AsQueryable();
    if (trackId.HasValue) query = query.Where(b => b.TrackId == trackId.Value);

    var items = await query.OrderByDescending(b => b.CreatedAt).ToListAsync(ct);
    return Results.Ok(new { items });
});

//Карты ритма - создание
app.MapPost("/api/beatmaps", async (CreateBeatmapDto dto, HttpRequest req, AppDbContext db, CancellationToken ct) =>
{
    var user = await ResolveUser(req, db, ct);
    if (user is null) return Results.Unauthorized();
    if (user.Role is not (Roles.Creator or Roles.Admin)) return Results.Forbid();

    var beatmap = new Beatmap
    {
        TrackId = dto.TrackId,
        Difficulty = dto.Difficulty,
        SpawnDataJson = dto.SpawnDataJson,
        CreatorId = user.Id
    };

    db.Beatmaps.Add(beatmap);
    await db.SaveChangesAsync(ct);
    return Results.Created($"/api/beatmaps/{beatmap.Id}", beatmap);
});

//Карта ритма - редактирование
app.MapPut("/api/beatmaps/{id:guid}", async (Guid id, UpdateBeatmapDto dto, HttpRequest req, AppDbContext db, CancellationToken ct) =>
{
    var user = await ResolveUser(req, db, ct);
    if (user is null) return Results.Unauthorized();

    var beatmap = await db.Beatmaps.FirstOrDefaultAsync(b => b.Id == id, ct);
    if (beatmap is null) return Results.NotFound();

    bool canEdit = user.Role == Roles.Admin || (user.Role == Roles.Creator && beatmap.CreatorId == user.Id);
    if (!canEdit) return Results.Forbid();

    beatmap.Difficulty = dto.Difficulty;
    beatmap.SpawnDataJson = dto.SpawnDataJson;

    await db.SaveChangesAsync(ct);
    return Results.NoContent();
});

//Результаты
app.MapPost("/api/results", async (CreateResultDto dto, HttpRequest req, AppDbContext db, CancellationToken ct) =>
{
    var user = await ResolveUser(req, db, ct);
    if (user is null) return Results.Unauthorized();

    var result = new GameResult
    {
        UserId = user.Id,
        BeatmapId = dto.BeatmapId,
        Score = dto.Score,
        Accuracy = dto.Accuracy,
        MaxCombo = dto.MaxCombo
    };

    db.GameResults.Add(result);
    await db.SaveChangesAsync(ct);
    return Results.Created($"/api/results/{result.Id}", result);
});

app.Run();

//Модели и контекст
static class Roles
{
    public const string Guest = "Guest";
    public const string Creator = "Creator";
    public const string Admin = "Admin";
}

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = Roles.Guest;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class Track
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public float Bpm { get; set; } = 120f;
    public float DurationSeconds { get; set; }
    public string AudioUrl { get; set; } = "";
    public Guid? UploadedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class Beatmap
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TrackId { get; set; }
    public Track? Track { get; set; }
    public string Difficulty { get; set; } = "Normal";
    public string SpawnDataJson { get; set; } = "[]";
    public Guid? CreatorId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class GameResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid BeatmapId { get; set; }
    public Beatmap? Beatmap { get; set; }
    public int Score { get; set; }
    public float Accuracy { get; set; }
    public int MaxCombo { get; set; }
    public DateTimeOffset PlayedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Track> Tracks => Set<Track>();
    public DbSet<Beatmap> Beatmaps => Set<Beatmap>();
    public DbSet<GameResult> GameResults => Set<GameResult>();
}

public record CreateUserDto(string Username, string PasswordHash, string Role);
public record UpdateTrackDto(string Title, string Artist, float Bpm);
public record CreateBeatmapDto(Guid TrackId, string Difficulty, string SpawnDataJson);
public record UpdateBeatmapDto(string Difficulty, string SpawnDataJson);
public record CreateResultDto(Guid BeatmapId, int Score, float Accuracy, int MaxCombo);