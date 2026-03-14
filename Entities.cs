using System.Numerics;

namespace NeonSiege;

public enum ScreenState
{
    Title,
    Playing,
    Paused,
    GameOver
}

public enum Difficulty
{
    Rookie,
    Standard,
    Overdrive
}

public enum EnemyKind
{
    Chaser,
    Brute,
    Shooter,
    Splitter,
    Boss
}

public enum PowerUpKind
{
    Heal,
    RapidFire,
    Bomb,
    SpreadShot,
    PierceShot,
    Shield
}

public sealed class Player
{
    public Vector2 Position { get; set; }
    public float Radius { get; set; } = 18f;
    public float Speed { get; set; } = 290f;
    public float MaxHealth { get; set; } = 100f;
    public float Health { get; set; } = 100f;
    public float InvulnerabilityTimer { get; set; }
    public float DashCooldown { get; set; }
    public float RapidFireTimer { get; set; }
    public float SpreadShotTimer { get; set; }
    public float PierceShotTimer { get; set; }
    public int ShieldHits { get; set; }
    public float BulletDamageBonus { get; set; }
    public float FireRateMultiplier { get; set; } = 1f;
    public int PermanentExtraBullets { get; set; }

    public Player(Vector2 position)
    {
        Position = position;
    }

    public void UpdateTimers(float dt)
    {
        if (InvulnerabilityTimer > 0f) InvulnerabilityTimer -= dt;
        if (DashCooldown > 0f) DashCooldown -= dt;
        if (RapidFireTimer > 0f) RapidFireTimer -= dt;
        if (SpreadShotTimer > 0f) SpreadShotTimer -= dt;
        if (PierceShotTimer > 0f) PierceShotTimer -= dt;
    }
}

public sealed class Enemy
{
    public Vector2 Position { get; set; }
    public float Radius { get; set; }
    public float Speed { get; set; }
    public float Health { get; set; }
    public float MaxHealth { get; set; }
    public float ContactDamage { get; set; }
    public float ShootCooldown { get; set; }
    public EnemyKind Kind { get; set; }
    public bool IsAlive { get; set; } = true;

    public Enemy(Vector2 position, EnemyKind kind)
    {
        Position = position;
        Kind = kind;

        switch (kind)
        {
            case EnemyKind.Brute:
                Radius = 28f;
                Speed = 118f;
                Health = 6f;
                ContactDamage = 22f;
                break;
            case EnemyKind.Shooter:
                Radius = 17f;
                Speed = 110f;
                Health = 3f;
                ContactDamage = 10f;
                ShootCooldown = 1.8f;
                break;
            case EnemyKind.Splitter:
                Radius = 21f;
                Speed = 150f;
                Health = 3f;
                ContactDamage = 13f;
                break;
            case EnemyKind.Boss:
                Radius = 54f;
                Speed = 82f;
                Health = 45f;
                ContactDamage = 34f;
                ShootCooldown = 2.2f;
                break;
            default:
                Radius = 15f;
                Speed = 175f;
                Health = 2f;
                ContactDamage = 12f;
                break;
        }

        MaxHealth = Health;
    }
}

public sealed class Bullet
{
    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
    public float Radius { get; set; } = 5f;
    public float Damage { get; set; } = 1f;
    public float Life { get; set; } = 1.2f;
    public int PierceRemaining { get; set; }
    public bool IsAlive { get; set; } = true;

    public Bullet(Vector2 position, Vector2 velocity)
    {
        Position = position;
        Velocity = velocity;
    }

    public void Update(float dt)
    {
        Position += Velocity * dt;
        Life -= dt;
        if (Life <= 0f) IsAlive = false;
    }
}

public sealed class EnemyBullet
{
    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
    public float Radius { get; set; } = 6f;
    public float Damage { get; set; } = 9f;
    public float Life { get; set; } = 3.2f;
    public bool IsAlive { get; set; } = true;
    public bool IsBossShot { get; set; }

    public EnemyBullet(Vector2 position, Vector2 velocity, bool isBossShot = false)
    {
        Position = position;
        Velocity = velocity;
        IsBossShot = isBossShot;
        if (isBossShot)
        {
            Radius = 8f;
            Damage = 13f;
        }
    }

    public void Update(float dt)
    {
        Position += Velocity * dt;
        Life -= dt;
        if (Life <= 0f) IsAlive = false;
    }
}

public sealed class Particle
{
    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
    public float Size { get; set; }
    public float Life { get; set; }
    public Color Color { get; set; }

    public Particle(Vector2 position, Vector2 velocity, float size, float life, Color color)
    {
        Position = position;
        Velocity = velocity;
        Size = size;
        Life = life;
        Color = color;
    }

    public void Update(float dt)
    {
        Position += Velocity * dt;
        Velocity *= 0.965f;
        Life -= dt;
    }
}

public sealed class PowerUp
{
    public Vector2 Position { get; set; }
    public float Radius { get; set; } = 14f;
    public PowerUpKind Kind { get; set; }
    public float Lifetime { get; set; } = 10f;
    public float Spin { get; set; }

    public PowerUp(Vector2 position, PowerUpKind kind)
    {
        Position = position;
        Kind = kind;
    }

    public void Update(float dt)
    {
        Lifetime -= dt;
        Spin += dt * 3f;
    }
}

public sealed class HighScoreData
{
    public int BestScore { get; set; }
    public float BestTime { get; set; }
    public int BestWave { get; set; }
    public int RunsPlayed { get; set; }
}
