using System.Drawing.Drawing2D;
using System.Media;
using System.Numerics;
using System.Text.Json;

namespace NeonSiege;

public sealed class GameForm : Form
{
    private const int WorldWidth = 1000;
    private const int WorldHeight = 700;
    private const string HighScoreFile = "neon_siege_highscore.json";

    private readonly System.Windows.Forms.Timer _gameTimer = new();
    private readonly Random _random = new();
    private readonly HashSet<Keys> _keysDown = new();

    private Player _player = new(new Vector2(WorldWidth / 2f, WorldHeight / 2f));
    private readonly List<Enemy> _enemies = [];
    private readonly List<Bullet> _bullets = [];
    private readonly List<EnemyBullet> _enemyBullets = [];
    private readonly List<Particle> _particles = [];
    private readonly List<PowerUp> _powerUps = [];

    private ScreenState _screen = ScreenState.Title;
    private Difficulty _difficulty = Difficulty.Standard;
    private HighScoreData _highScore = new();
    private Vector2 _mousePosition = new(WorldWidth / 2f, WorldHeight / 2f);

    private bool _isFiring;
    private bool _screenShakeEnabled = true;
    private float _shootTimer;
    private float _spawnTimer;
    private float _waveTimer;
    private float _survivalTime;
    private float _screenShake;
    private float _flashTimer;
    private float _notificationTimer;
    private int _score;
    private int _wave = 1;
    private int _upgradeTier;
    private int _bossWaveSpawnedFor;
    private int _kills;
    private string _notification = "Press Enter to Start";

    public GameForm()
    {
        Text = "Neon Siege";
        ClientSize = new Size(WorldWidth, WorldHeight);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(12, 14, 24);
        DoubleBuffered = true;
        KeyPreview = true;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        Cursor = Cursors.Cross;

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

        _gameTimer.Interval = 16;
        _gameTimer.Tick += (_, _) => GameLoop();
        _gameTimer.Start();

        LoadHighScore();

        KeyDown += HandleKeyDown;
        KeyUp += HandleKeyUp;
        MouseMove += (_, e) => _mousePosition = new Vector2(e.X, e.Y);
        MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Left && _screen == ScreenState.Playing)
                _isFiring = true;
        };
        MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                _isFiring = false;
        };
    }

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        _keysDown.Add(e.KeyCode);

        if (_screen == ScreenState.Title)
        {
            if (e.KeyCode == Keys.Enter)
            {
                StartGame();
                return;
            }

            if (e.KeyCode == Keys.Tab || e.KeyCode == Keys.Right)
            {
                CycleDifficulty(1);
                return;
            }

            if (e.KeyCode == Keys.Left)
            {
                CycleDifficulty(-1);
                return;
            }

            if (e.KeyCode == Keys.H)
            {
                _screenShakeEnabled = !_screenShakeEnabled;
                SetNotification(_screenShakeEnabled ? "Screen shake enabled." : "Screen shake disabled.");
            }
        }

        if (_screen == ScreenState.GameOver && e.KeyCode == Keys.R)
        {
            StartGame();
            return;
        }

        if (_screen == ScreenState.Playing)
        {
            if (e.KeyCode == Keys.Space)
            {
                TryDash();
                return;
            }

            if (e.KeyCode == Keys.P || e.KeyCode == Keys.Escape)
            {
                _screen = ScreenState.Paused;
                _isFiring = false;
                return;
            }
        }
        else if (_screen == ScreenState.Paused)
        {
            if (e.KeyCode == Keys.P || e.KeyCode == Keys.Escape)
            {
                _screen = ScreenState.Playing;
                return;
            }

            if (e.KeyCode == Keys.R)
            {
                StartGame();
                return;
            }
        }
    }

    private void HandleKeyUp(object? sender, KeyEventArgs e)
    {
        _keysDown.Remove(e.KeyCode);
    }

    private void CycleDifficulty(int direction)
    {
        var values = Enum.GetValues<Difficulty>();
        var index = Array.IndexOf(values, _difficulty);
        index = (index + direction + values.Length) % values.Length;
        _difficulty = values[index];
        SetNotification($"Difficulty: {_difficulty}");
    }

    private void StartGame()
    {
        _screen = ScreenState.Playing;
        _player = new Player(new Vector2(WorldWidth / 2f, WorldHeight / 2f));
        _enemies.Clear();
        _bullets.Clear();
        _enemyBullets.Clear();
        _particles.Clear();
        _powerUps.Clear();

        _score = 0;
        _wave = 1;
        _upgradeTier = 0;
        _bossWaveSpawnedFor = 0;
        _kills = 0;
        _shootTimer = 0f;
        _spawnTimer = 0.65f;
        _waveTimer = 0f;
        _survivalTime = 0f;
        _screenShake = 0f;
        _flashTimer = 0f;
        _notificationTimer = 0f;
        _notification = string.Empty;
        _isFiring = false;

        switch (_difficulty)
        {
            case Difficulty.Rookie:
                _player.MaxHealth = 120f;
                _player.Health = 120f;
                _player.Speed = 310f;
                break;
            case Difficulty.Overdrive:
                _player.MaxHealth = 90f;
                _player.Health = 90f;
                _player.Speed = 300f;
                break;
            default:
                _player.MaxHealth = 100f;
                _player.Health = 100f;
                _player.Speed = 290f;
                break;
        }

        SetNotification("Survive. Upgrade. Don't become paste.");
    }

    private void GameLoop()
    {
        const float dt = 1f / 60f;

        if (_screen == ScreenState.Playing)
            UpdateGame(dt);
        else
            UpdateAmbient(dt);

        if (_screenShake > 0f)
            _screenShake = Math.Max(0f, _screenShake - dt * 30f);

        if (_flashTimer > 0f)
            _flashTimer = Math.Max(0f, _flashTimer - dt);

        if (_notificationTimer > 0f)
            _notificationTimer = Math.Max(0f, _notificationTimer - dt);

        Invalidate();
    }

    private void UpdateAmbient(float dt)
    {
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            _particles[i].Update(dt);
            if (_particles[i].Life <= 0f)
                _particles.RemoveAt(i);
        }

        if (_particles.Count < 50 && _random.NextDouble() < 0.20)
        {
            var position = new Vector2(_random.Next(0, WorldWidth), _random.Next(0, WorldHeight));
            var velocity = new Vector2((float)(_random.NextDouble() - 0.5) * 12f, (float)(_random.NextDouble() - 0.5) * 12f);
            _particles.Add(new Particle(position, velocity, _random.Next(2, 5), 1.4f, Color.FromArgb(100, 0, 255, 255)));
        }
    }

    private void UpdateGame(float dt)
    {
        _survivalTime += dt;
        _waveTimer += dt;
        _shootTimer -= dt;
        _spawnTimer -= dt;
        _player.UpdateTimers(dt);

        ApplyScoreUpgradeTiers();

        if (_waveTimer >= 16f)
        {
            _wave++;
            _waveTimer = 0f;
            SetNotification($"Wave {_wave}");
            SpawnWaveBurst();

            if (_wave % 5 == 0 && _bossWaveSpawnedFor != _wave)
            {
                SpawnBoss();
                _bossWaveSpawnedFor = _wave;
                SetNotification($"Boss wave {_wave} incoming.");
            }
        }

        HandleMovement(dt);
        HandleShooting();

        if (_spawnTimer <= 0f)
        {
            SpawnEnemy();
            _spawnTimer = GetSpawnInterval();
        }

        UpdateBullets(dt);
        UpdateEnemyBullets(dt);
        UpdateEnemies(dt);
        UpdateParticles(dt);
        UpdatePowerUps(dt);
        HandleCollisions();

        if (_player.Health <= 0f)
        {
            EndRun();
        }
    }

    private float GetDifficultyEnemyHealthMultiplier()
    {
        return _difficulty switch
        {
            Difficulty.Rookie => 0.9f,
            Difficulty.Overdrive => 1.2f,
            _ => 1f
        };
    }

    private float GetDifficultyEnemySpeedMultiplier()
    {
        return _difficulty switch
        {
            Difficulty.Rookie => 0.92f,
            Difficulty.Overdrive => 1.15f,
            _ => 1f
        };
    }

    private float GetDifficultyEnemyDamageMultiplier()
    {
        return _difficulty switch
        {
            Difficulty.Rookie => 0.88f,
            Difficulty.Overdrive => 1.18f,
            _ => 1f
        };
    }

    private float GetSpawnInterval()
    {
        float interval = 1.1f - ((_wave - 1) * 0.07f);

        if (_difficulty == Difficulty.Rookie)
            interval += 0.12f;
        else if (_difficulty == Difficulty.Overdrive)
            interval -= 0.08f;

        return Math.Max(0.22f, interval);
    }

    private void ApplyScoreUpgradeTiers()
    {
        while (true)
        {
            if (_upgradeTier == 0 && _score >= 250)
            {
                _upgradeTier++;
                _player.BulletDamageBonus += 0.6f;
                SetNotification("Upgrade: overcharged rounds.");
            }
            else if (_upgradeTier == 1 && _score >= 700)
            {
                _upgradeTier++;
                _player.FireRateMultiplier = 0.88f;
                SetNotification("Upgrade: faster firing.");
            }
            else if (_upgradeTier == 2 && _score >= 1300)
            {
                _upgradeTier++;
                _player.PermanentExtraBullets += 2;
                SetNotification("Upgrade: permanent twin spread.");
            }
            else if (_upgradeTier == 3 && _score >= 2200)
            {
                _upgradeTier++;
                _player.PierceShotTimer = Math.Max(_player.PierceShotTimer, 9999f);
                SetNotification("Upgrade: piercing rounds unlocked.");
            }
            else if (_upgradeTier == 4 && _score >= 3400)
            {
                _upgradeTier++;
                _player.BulletDamageBonus += 0.8f;
                _player.FireRateMultiplier = 0.76f;
                SetNotification("Upgrade: meltdown mode.");
            }
            else
            {
                break;
            }
        }
    }

    private void HandleMovement(float dt)
    {
        Vector2 move = Vector2.Zero;

        if (_keysDown.Contains(Keys.W) || _keysDown.Contains(Keys.Up))
            move.Y -= 1f;
        if (_keysDown.Contains(Keys.S) || _keysDown.Contains(Keys.Down))
            move.Y += 1f;
        if (_keysDown.Contains(Keys.A) || _keysDown.Contains(Keys.Left))
            move.X -= 1f;
        if (_keysDown.Contains(Keys.D) || _keysDown.Contains(Keys.Right))
            move.X += 1f;

        if (move != Vector2.Zero)
        {
            move = Vector2.Normalize(move);
            _player.Position += move * _player.Speed * dt;
        }

        _player.Position = new Vector2(
            Math.Clamp(_player.Position.X, _player.Radius, WorldWidth - _player.Radius),
            Math.Clamp(_player.Position.Y, _player.Radius, WorldHeight - _player.Radius));
    }

    private void HandleShooting()
    {
        if (!_isFiring || _shootTimer > 0f)
            return;

        Vector2 aim = _mousePosition - _player.Position;
        if (aim.LengthSquared() < 1f)
            aim = new Vector2(1f, 0f);

        aim = Vector2.Normalize(aim);
        float damage = 1f + _player.BulletDamageBonus;
        int pierce = _player.PierceShotTimer > 0f ? 1 : 0;
        FireBullet(aim, damage, pierce);

        int extraBullets = _player.PermanentExtraBullets;
        if (_player.SpreadShotTimer > 0f)
            extraBullets += 2;

        if (extraBullets > 0)
        {
            float[] angles = extraBullets switch
            {
                1 => [0.16f],
                2 => [-0.16f, 0.16f],
                3 => [-0.22f, 0f, 0.22f],
                4 => [-0.28f, -0.10f, 0.10f, 0.28f],
                _ => [-0.32f, -0.18f, -0.06f, 0.06f, 0.18f, 0.32f]
            };

            foreach (float angle in angles)
            {
                FireBullet(Rotate(aim, angle), damage * 0.82f, pierce);
            }
        }

        SpawnMuzzleFlash(_player.Position + aim * 20f);

        float cooldown = 0.16f * _player.FireRateMultiplier;
        if (_player.RapidFireTimer > 0f)
            cooldown *= 0.5f;
        _shootTimer = Math.Max(0.045f, cooldown);
    }

    private void FireBullet(Vector2 direction, float damage, int pierce)
    {
        var bullet = new Bullet(_player.Position + direction * 20f, direction * 670f)
        {
            Damage = damage,
            PierceRemaining = pierce,
            Radius = 5f + Math.Min(2f, _player.BulletDamageBonus * 0.35f)
        };

        _bullets.Add(bullet);
    }

    private void TryDash()
    {
        if (_player.DashCooldown > 0f)
            return;

        Vector2 dashDirection = Vector2.Zero;

        if (_keysDown.Contains(Keys.W) || _keysDown.Contains(Keys.Up))
            dashDirection.Y -= 1f;
        if (_keysDown.Contains(Keys.S) || _keysDown.Contains(Keys.Down))
            dashDirection.Y += 1f;
        if (_keysDown.Contains(Keys.A) || _keysDown.Contains(Keys.Left))
            dashDirection.X -= 1f;
        if (_keysDown.Contains(Keys.D) || _keysDown.Contains(Keys.Right))
            dashDirection.X += 1f;

        if (dashDirection == Vector2.Zero)
            dashDirection = _mousePosition - _player.Position;

        if (dashDirection.LengthSquared() < 1f)
            dashDirection = new Vector2(1f, 0f);

        dashDirection = Vector2.Normalize(dashDirection);

        _player.Position += dashDirection * 125f;
        _player.Position = new Vector2(
            Math.Clamp(_player.Position.X, _player.Radius, WorldWidth - _player.Radius),
            Math.Clamp(_player.Position.Y, _player.Radius, WorldHeight - _player.Radius));

        _player.DashCooldown = 1.2f;
        _player.InvulnerabilityTimer = 0.22f;

        for (int i = 0; i < 18; i++)
        {
            var velocity = RandomDirection() * _random.Next(90, 240);
            _particles.Add(new Particle(_player.Position, velocity, _random.Next(2, 5), 0.4f, Color.Cyan));
        }

        BumpScreenShake(4f);
    }

    private void UpdateBullets(float dt)
    {
        for (int i = _bullets.Count - 1; i >= 0; i--)
        {
            Bullet bullet = _bullets[i];
            bullet.Update(dt);

            if (bullet.Position.X < -40 || bullet.Position.X > WorldWidth + 40 ||
                bullet.Position.Y < -40 || bullet.Position.Y > WorldHeight + 40)
            {
                bullet.IsAlive = false;
            }

            if (!bullet.IsAlive)
                _bullets.RemoveAt(i);
        }
    }

    private void UpdateEnemyBullets(float dt)
    {
        for (int i = _enemyBullets.Count - 1; i >= 0; i--)
        {
            var bullet = _enemyBullets[i];
            bullet.Update(dt);

            if (bullet.Position.X < -60 || bullet.Position.X > WorldWidth + 60 ||
                bullet.Position.Y < -60 || bullet.Position.Y > WorldHeight + 60)
            {
                bullet.IsAlive = false;
            }

            if (!bullet.IsAlive)
                _enemyBullets.RemoveAt(i);
        }
    }

    private void UpdateEnemies(float dt)
    {
        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            Enemy enemy = _enemies[i];
            Vector2 toPlayer = _player.Position - enemy.Position;
            float distanceSq = toPlayer.LengthSquared();

            if (distanceSq > 1f)
                toPlayer = Vector2.Normalize(toPlayer);

            switch (enemy.Kind)
            {
                case EnemyKind.Shooter:
                    if (distanceSq > 180f * 180f)
                    {
                        enemy.Position += toPlayer * enemy.Speed * dt;
                    }
                    else if (distanceSq < 120f * 120f)
                    {
                        enemy.Position -= toPlayer * enemy.Speed * 0.70f * dt;
                    }

                    enemy.ShootCooldown -= dt;
                    if (enemy.ShootCooldown <= 0f)
                    {
                        FireEnemyBullet(enemy.Position, toPlayer * 280f, false);
                        enemy.ShootCooldown = Math.Max(0.8f, 1.7f - (_wave * 0.04f));
                    }
                    break;

                case EnemyKind.Boss:
                    enemy.Position += toPlayer * enemy.Speed * dt;
                    enemy.ShootCooldown -= dt;
                    if (enemy.ShootCooldown <= 0f)
                    {
                        FireBossSpread(enemy.Position, toPlayer);
                        enemy.ShootCooldown = 1.4f;
                    }
                    break;

                default:
                    enemy.Position += toPlayer * enemy.Speed * dt;
                    break;
            }

            if (!enemy.IsAlive)
                _enemies.RemoveAt(i);
        }
    }

    private void FireEnemyBullet(Vector2 origin, Vector2 velocity, bool isBossShot)
    {
        _enemyBullets.Add(new EnemyBullet(origin, velocity, isBossShot));
    }

    private void FireBossSpread(Vector2 origin, Vector2 aim)
    {
        if (aim.LengthSquared() < 1f)
            aim = new Vector2(1f, 0f);

        aim = Vector2.Normalize(aim);

        for (int i = -2; i <= 2; i++)
        {
            Vector2 direction = Rotate(aim, i * 0.22f);
            FireEnemyBullet(origin, direction * 320f, true);
        }

        BumpScreenShake(4f);
    }

    private void UpdateParticles(float dt)
    {
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            _particles[i].Update(dt);
            if (_particles[i].Life <= 0f)
                _particles.RemoveAt(i);
        }
    }

    private void UpdatePowerUps(float dt)
    {
        for (int i = _powerUps.Count - 1; i >= 0; i--)
        {
            _powerUps[i].Update(dt);
            if (_powerUps[i].Lifetime <= 0f)
                _powerUps.RemoveAt(i);
        }
    }

    private void HandleCollisions()
    {
        for (int i = _bullets.Count - 1; i >= 0; i--)
        {
            var bullet = _bullets[i];

            for (int j = _enemies.Count - 1; j >= 0; j--)
            {
                var enemy = _enemies[j];
                if (!enemy.IsAlive)
                    continue;

                if (!IsColliding(bullet.Position, bullet.Radius, enemy.Position, enemy.Radius))
                    continue;

                enemy.Health -= bullet.Damage;
                SpawnHitParticles(bullet.Position, GetEnemyColor(enemy.Kind));

                if (enemy.Health <= 0f)
                {
                    KillEnemy(enemy, j);
                }

                if (bullet.PierceRemaining > 0)
                {
                    bullet.PierceRemaining--;
                }
                else
                {
                    bullet.IsAlive = false;
                    _bullets.RemoveAt(i);
                    break;
                }
            }
        }

        for (int i = _enemyBullets.Count - 1; i >= 0; i--)
        {
            var bullet = _enemyBullets[i];
            if (!IsColliding(_player.Position, _player.Radius, bullet.Position, bullet.Radius))
                continue;

            bullet.IsAlive = false;
            _enemyBullets.RemoveAt(i);
            DamagePlayer(bullet.Damage, bullet.IsBossShot ? 7f : 4f);
        }

        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            var enemy = _enemies[i];
            if (!enemy.IsAlive)
                continue;

            if (!IsColliding(_player.Position, _player.Radius, enemy.Position, enemy.Radius))
                continue;

            if (_player.InvulnerabilityTimer > 0f)
                continue;

            if (_player.ShieldHits > 0)
            {
                _player.ShieldHits--;
                enemy.IsAlive = false;
                SpawnDeathParticles(enemy.Position, enemy.Kind);
                BumpScreenShake(5f);
                _flashTimer = 0.07f;
            }
            else
            {
                DamagePlayer(enemy.ContactDamage, enemy.Kind == EnemyKind.Boss ? 9f : 5f);
                enemy.IsAlive = false;
                SpawnDeathParticles(enemy.Position, enemy.Kind);
            }
        }

        for (int i = _powerUps.Count - 1; i >= 0; i--)
        {
            var powerUp = _powerUps[i];
            if (!IsColliding(_player.Position, _player.Radius, powerUp.Position, powerUp.Radius))
                continue;

            ApplyPowerUp(powerUp.Kind);
            _powerUps.RemoveAt(i);
        }
    }

    private void KillEnemy(Enemy enemy, int enemyIndex)
    {
        enemy.IsAlive = false;
        _kills++;

        int points = enemy.Kind switch
        {
            EnemyKind.Brute => 140,
            EnemyKind.Shooter => 110,
            EnemyKind.Splitter => 95,
            EnemyKind.Boss => 850,
            _ => 60
        };

        _score += points;
        SpawnDeathParticles(enemy.Position, enemy.Kind);

        if (enemy.Kind == EnemyKind.Splitter)
        {
            for (int i = 0; i < 2; i++)
            {
                var child = new Enemy(enemy.Position + RandomDirection() * _random.Next(8, 20), EnemyKind.Chaser);
                child.Speed *= 1.20f;
                child.Health = 1.4f;
                child.MaxHealth = child.Health;
                _enemies.Add(child);
            }
        }

        if (enemy.Kind == EnemyKind.Boss)
        {
            SetNotification("Boss destroyed.");
            for (int i = 0; i < 3; i++)
                SpawnPowerUp(enemy.Position + RandomDirection() * _random.Next(0, 40));
        }
        else if (_random.NextDouble() < 0.12)
        {
            SpawnPowerUp(enemy.Position);
        }

        if (enemyIndex >= 0 && enemyIndex < _enemies.Count)
            _enemies.RemoveAt(enemyIndex);

        BumpScreenShake(enemy.Kind == EnemyKind.Boss ? 10f : 3f);
    }

    private void DamagePlayer(float amount, float shake)
    {
        if (_player.InvulnerabilityTimer > 0f)
            return;

        _player.Health = Math.Max(0f, _player.Health - amount);
        _player.InvulnerabilityTimer = 0.68f;
        BumpScreenShake(shake);
        _flashTimer = 0.14f;

        for (int i = 0; i < 14; i++)
        {
            _particles.Add(new Particle(_player.Position, RandomDirection() * _random.Next(70, 220), _random.Next(2, 5), 0.45f, Color.Red));
        }
    }

    private void ApplyPowerUp(PowerUpKind kind)
    {
        switch (kind)
        {
            case PowerUpKind.Heal:
                _player.Health = Math.Min(_player.MaxHealth, _player.Health + 30f);
                SetNotification("Power-up: heal.");
                BurstAroundPlayer(Color.LimeGreen, 14);
                break;

            case PowerUpKind.RapidFire:
                _player.RapidFireTimer = Math.Max(_player.RapidFireTimer, 7f);
                SetNotification("Power-up: rapid fire.");
                BurstAroundPlayer(Color.Gold, 14);
                break;

            case PowerUpKind.Bomb:
                int bonus = 0;
                for (int i = _enemies.Count - 1; i >= 0; i--)
                {
                    var enemy = _enemies[i];
                    if (!enemy.IsAlive || enemy.Kind == EnemyKind.Boss)
                        continue;

                    bonus += enemy.Kind == EnemyKind.Brute ? 60 : 25;
                    SpawnDeathParticles(enemy.Position, enemy.Kind);
                    _enemies.RemoveAt(i);
                }

                _score += bonus;
                SetNotification("Power-up: bomb.");
                BurstAroundPlayer(Color.DeepSkyBlue, 24);
                BumpScreenShake(9f);
                break;

            case PowerUpKind.SpreadShot:
                _player.SpreadShotTimer = Math.Max(_player.SpreadShotTimer, 10f);
                SetNotification("Power-up: spread shot.");
                BurstAroundPlayer(Color.HotPink, 16);
                break;

            case PowerUpKind.PierceShot:
                _player.PierceShotTimer = Math.Max(_player.PierceShotTimer, 10f);
                SetNotification("Power-up: piercing shots.");
                BurstAroundPlayer(Color.MediumPurple, 16);
                break;

            case PowerUpKind.Shield:
                _player.ShieldHits = Math.Min(3, _player.ShieldHits + 1);
                SetNotification("Power-up: shield.");
                BurstAroundPlayer(Color.Cyan, 18);
                break;
        }

        SystemSounds.Asterisk.Play();
    }

    private void BurstAroundPlayer(Color color, int count)
    {
        for (int i = 0; i < count; i++)
        {
            _particles.Add(new Particle(_player.Position, RandomDirection() * _random.Next(60, 220), _random.Next(2, 6), 0.55f, color));
        }
    }

    private void SpawnEnemy()
    {
        if (_screen != ScreenState.Playing)
            return;

        EnemyKind kind = EnemyKind.Chaser;
        double roll = _random.NextDouble();

        if (_wave >= 3 && roll < 0.16)
            kind = EnemyKind.Shooter;
        else if (_wave >= 4 && roll < 0.28)
            kind = EnemyKind.Splitter;
        else if (_wave >= 2 && roll < 0.43)
            kind = EnemyKind.Brute;

        var enemy = new Enemy(SpawnPositionOutsideArena(), kind);

        float healthMult = GetDifficultyEnemyHealthMultiplier();
        float speedMult = GetDifficultyEnemySpeedMultiplier();
        float damageMult = GetDifficultyEnemyDamageMultiplier();

        enemy.Health *= healthMult * (1f + (_wave - 1) * 0.06f);
        enemy.MaxHealth = enemy.Health;
        enemy.Speed *= speedMult * (1f + (_wave - 1) * 0.012f);
        enemy.ContactDamage *= damageMult;

        _enemies.Add(enemy);
    }

    private void SpawnWaveBurst()
    {
        int amount = Math.Min(3 + _wave, 10);
        for (int i = 0; i < amount; i++)
            SpawnEnemy();
    }

    private void SpawnBoss()
    {
        var boss = new Enemy(SpawnPositionOutsideArena(), EnemyKind.Boss);
        boss.Health *= GetDifficultyEnemyHealthMultiplier() * (1f + (_wave - 1) * 0.10f);
        boss.MaxHealth = boss.Health;
        boss.Speed *= GetDifficultyEnemySpeedMultiplier();
        boss.ContactDamage *= GetDifficultyEnemyDamageMultiplier();
        _enemies.Add(boss);
    }

    private void SpawnPowerUp(Vector2 position)
    {
        int roll = _random.Next(0, 6);
        PowerUpKind kind = roll switch
        {
            0 => PowerUpKind.Heal,
            1 => PowerUpKind.RapidFire,
            2 => PowerUpKind.Bomb,
            3 => PowerUpKind.SpreadShot,
            4 => PowerUpKind.PierceShot,
            _ => PowerUpKind.Shield
        };

        _powerUps.Add(new PowerUp(position, kind));
    }

    private Vector2 SpawnPositionOutsideArena()
    {
        int side = _random.Next(4);
        return side switch
        {
            0 => new Vector2(-30f, _random.Next(0, WorldHeight)),
            1 => new Vector2(WorldWidth + 30f, _random.Next(0, WorldHeight)),
            2 => new Vector2(_random.Next(0, WorldWidth), -30f),
            _ => new Vector2(_random.Next(0, WorldWidth), WorldHeight + 30f)
        };
    }

    private Vector2 RandomDirection()
    {
        double angle = _random.NextDouble() * Math.PI * 2.0;
        return new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
    }

    private static Vector2 Rotate(Vector2 vector, float radians)
    {
        float cos = MathF.Cos(radians);
        float sin = MathF.Sin(radians);
        return new Vector2(vector.X * cos - vector.Y * sin, vector.X * sin + vector.Y * cos);
    }

    private static bool IsColliding(Vector2 aPos, float aRadius, Vector2 bPos, float bRadius)
    {
        float radius = aRadius + bRadius;
        return Vector2.DistanceSquared(aPos, bPos) <= radius * radius;
    }

    private void SpawnMuzzleFlash(Vector2 position)
    {
        for (int i = 0; i < 5; i++)
        {
            _particles.Add(new Particle(position, RandomDirection() * _random.Next(70, 190), 3f, 0.18f, Color.Gold));
        }
    }

    private void SpawnHitParticles(Vector2 position, Color color)
    {
        for (int i = 0; i < 8; i++)
        {
            _particles.Add(new Particle(position, RandomDirection() * _random.Next(40, 170), _random.Next(2, 5), 0.28f, color));
        }
    }

    private void SpawnDeathParticles(Vector2 position, EnemyKind kind)
    {
        Color color = GetEnemyColor(kind);
        int count = kind == EnemyKind.Boss ? 40 : kind == EnemyKind.Brute ? 20 : 14;

        for (int i = 0; i < count; i++)
        {
            _particles.Add(new Particle(position, RandomDirection() * _random.Next(60, 280), _random.Next(2, 6), 0.55f, color));
        }
    }

    private static Color GetEnemyColor(EnemyKind kind)
    {
        return kind switch
        {
            EnemyKind.Brute => Color.Orange,
            EnemyKind.Shooter => Color.MediumPurple,
            EnemyKind.Splitter => Color.SpringGreen,
            EnemyKind.Boss => Color.Red,
            _ => Color.HotPink
        };
    }

    private void BumpScreenShake(float amount)
    {
        if (_screenShakeEnabled)
            _screenShake = Math.Max(_screenShake, amount);
    }

    private void EndRun()
    {
        _screen = ScreenState.GameOver;
        _isFiring = false;

        _highScore.RunsPlayed++;
        if (_score > _highScore.BestScore)
            _highScore.BestScore = _score;
        if (_survivalTime > _highScore.BestTime)
            _highScore.BestTime = _survivalTime;
        if (_wave > _highScore.BestWave)
            _highScore.BestWave = _wave;

        SaveHighScore();
        SetNotification("Run over.");
        SystemSounds.Hand.Play();
    }

    private void SetNotification(string message)
    {
        _notification = message;
        _notificationTimer = 2.4f;
    }

    private void LoadHighScore()
    {
        try
        {
            if (!File.Exists(HighScoreFile))
                return;

            string json = File.ReadAllText(HighScoreFile);
            HighScoreData? loaded = JsonSerializer.Deserialize<HighScoreData>(json);
            if (loaded is not null)
                _highScore = loaded;
        }
        catch
        {
            _highScore = new HighScoreData();
        }
    }

    private void SaveHighScore()
    {
        try
        {
            string json = JsonSerializer.Serialize(_highScore, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(HighScoreFile, json);
        }
        catch
        {
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        if (_screen == ScreenState.Title)
        {
            DrawBackground(g);
            DrawParticles(g);
            DrawTitleScreen(g);
            return;
        }

        DrawBackground(g);

        if (_screenShake > 0f)
        {
            float offsetX = (float)((_random.NextDouble() - 0.5) * _screenShake * 2f);
            float offsetY = (float)((_random.NextDouble() - 0.5) * _screenShake * 2f);
            g.TranslateTransform(offsetX, offsetY);
        }

        DrawPowerUps(g);
        DrawParticles(g);
        DrawBullets(g);
        DrawEnemyBullets(g);
        DrawEnemies(g);
        DrawPlayer(g);
        DrawCrosshair(g);

        g.ResetTransform();

        DrawHud(g);

        if (_screen == ScreenState.Paused)
            DrawPauseOverlay(g);

        if (_screen == ScreenState.GameOver)
            DrawGameOverOverlay(g);

        if (_flashTimer > 0f)
        {
            int alpha = (int)(_flashTimer / 0.14f * 90f);
            using SolidBrush flashBrush = new(Color.FromArgb(Math.Clamp(alpha, 0, 90), 255, 40, 40));
            g.FillRectangle(flashBrush, ClientRectangle);
        }
    }

    private void DrawBackground(Graphics g)
    {
        using LinearGradientBrush brush = new(
            new Rectangle(0, 0, WorldWidth, WorldHeight),
            Color.FromArgb(8, 10, 18),
            Color.FromArgb(18, 20, 36),
            LinearGradientMode.Vertical);

        g.FillRectangle(brush, ClientRectangle);

        using Pen gridPen = new(Color.FromArgb(26, 60, 190, 255), 1f);

        for (int x = 0; x < WorldWidth; x += 40)
            g.DrawLine(gridPen, x, 0, x, WorldHeight);

        for (int y = 0; y < WorldHeight; y += 40)
            g.DrawLine(gridPen, 0, y, WorldWidth, y);
    }

    private void DrawTitleScreen(Graphics g)
    {
        using Font titleFont = new("Segoe UI", 34, FontStyle.Bold);
        using Font subtitleFont = new("Segoe UI", 13, FontStyle.Regular);
        using Font infoFont = new("Segoe UI", 14, FontStyle.Bold);
        using Font smallFont = new("Segoe UI", 11, FontStyle.Regular);

        DrawCenteredText(g, "NEON SIEGE", titleFont, Brushes.Cyan, 104);
        DrawCenteredText(g, "An actual little arcade shooter in C# and Windows Forms", subtitleFont, Brushes.WhiteSmoke, 160);

        var lines = new[]
        {
            "WASD / Arrow Keys  - Move",
            "Mouse              - Aim",
            "Hold Left Click    - Shoot",
            "Space              - Dash",
            "P / Esc            - Pause",
            "Enter              - Start",
            "",
            "Tab / Left / Right - Change Difficulty",
            "H                  - Toggle Screen Shake"
        };

        int y = 240;
        foreach (string line in lines)
        {
            DrawCenteredText(g, line, infoFont, Brushes.WhiteSmoke, y);
            y += 34;
        }

        DrawCenteredText(g, $"Difficulty: {_difficulty}", infoFont, Brushes.Gold, 544);
        DrawCenteredText(g, $"Best Score: {_highScore.BestScore}   Best Wave: {_highScore.BestWave}   Best Time: {_highScore.BestTime:0.0}s", smallFont, Brushes.White, 590);
        DrawCenteredText(g, $"Runs Played: {_highScore.RunsPlayed}", smallFont, Brushes.White, 622);

        if (_notificationTimer > 0f)
            DrawCenteredText(g, _notification, infoFont, Brushes.Cyan, 660);
    }

    private void DrawPlayer(Graphics g)
    {
        bool blink = _player.InvulnerabilityTimer > 0f && ((int)(_player.InvulnerabilityTimer * 20f) % 2 == 0);
        if (blink)
            return;

        float x = _player.Position.X;
        float y = _player.Position.Y;

        using SolidBrush glow = new(Color.FromArgb(60, 0, 255, 255));
        g.FillEllipse(glow, x - 28, y - 28, 56, 56);

        using SolidBrush bodyBrush = new(Color.FromArgb(0, 255, 255));
        g.FillEllipse(bodyBrush, x - _player.Radius, y - _player.Radius, _player.Radius * 2, _player.Radius * 2);

        Vector2 aim = _mousePosition - _player.Position;
        if (aim.LengthSquared() < 1f)
            aim = new Vector2(1f, 0f);

        aim = Vector2.Normalize(aim);
        using Pen barrelPen = new(Color.White, 5f);
        g.DrawLine(barrelPen, x, y, x + aim.X * 24f, y + aim.Y * 24f);

        if (_player.ShieldHits > 0)
        {
            using Pen shieldPen = new(Color.Cyan, 3f);
            g.DrawEllipse(shieldPen, x - 24, y - 24, 48, 48);
        }
    }

    private void DrawEnemies(Graphics g)
    {
        foreach (Enemy enemy in _enemies.Where(e => e.IsAlive))
        {
            Color color = GetEnemyColor(enemy.Kind);

            using SolidBrush glow = new(Color.FromArgb(enemy.Kind == EnemyKind.Boss ? 80 : 50, color));
            g.FillEllipse(glow, enemy.Position.X - enemy.Radius - 10, enemy.Position.Y - enemy.Radius - 10, enemy.Radius * 2 + 20, enemy.Radius * 2 + 20);

            using SolidBrush brush = new(color);
            g.FillEllipse(brush, enemy.Position.X - enemy.Radius, enemy.Position.Y - enemy.Radius, enemy.Radius * 2, enemy.Radius * 2);

            using SolidBrush eyeBrush = new(Color.White);
            g.FillEllipse(eyeBrush, enemy.Position.X - 6, enemy.Position.Y - 4, 4, 4);
            g.FillEllipse(eyeBrush, enemy.Position.X + 2, enemy.Position.Y - 4, 4, 4);

            if (enemy.Kind == EnemyKind.Brute || enemy.Kind == EnemyKind.Boss)
                DrawEnemyHealthBar(g, enemy);
        }
    }

    private void DrawEnemyHealthBar(Graphics g, Enemy enemy)
    {
        float width = enemy.Kind == EnemyKind.Boss ? 100f : 50f;
        float height = 7f;
        float x = enemy.Position.X - width / 2f;
        float y = enemy.Position.Y - enemy.Radius - 18f;

        using SolidBrush bgBrush = new(Color.FromArgb(70, 255, 255, 255));
        using SolidBrush healthBrush = new(enemy.Kind == EnemyKind.Boss ? Color.Red : Color.Orange);

        g.FillRectangle(bgBrush, x, y, width, height);
        float currentWidth = width * (enemy.Health / enemy.MaxHealth);
        g.FillRectangle(healthBrush, x, y, currentWidth, height);
        g.DrawRectangle(Pens.White, x, y, width, height);
    }

    private void DrawBullets(Graphics g)
    {
        foreach (Bullet bullet in _bullets)
        {
            using SolidBrush brush = new(_player.PierceShotTimer > 0f ? Color.MediumPurple : Color.Gold);
            g.FillEllipse(brush, bullet.Position.X - bullet.Radius, bullet.Position.Y - bullet.Radius, bullet.Radius * 2, bullet.Radius * 2);
        }
    }

    private void DrawEnemyBullets(Graphics g)
    {
        foreach (EnemyBullet bullet in _enemyBullets)
        {
            Color color = bullet.IsBossShot ? Color.Red : Color.MediumPurple;
            using SolidBrush brush = new(color);
            g.FillEllipse(brush, bullet.Position.X - bullet.Radius, bullet.Position.Y - bullet.Radius, bullet.Radius * 2, bullet.Radius * 2);
        }
    }

    private void DrawParticles(Graphics g)
    {
        foreach (Particle particle in _particles)
        {
            int alpha = (int)(Math.Clamp(particle.Life, 0f, 1f) * 255);
            using SolidBrush brush = new(Color.FromArgb(alpha, particle.Color));
            g.FillEllipse(brush, particle.Position.X - particle.Size / 2f, particle.Position.Y - particle.Size / 2f, particle.Size, particle.Size);
        }
    }

    private void DrawPowerUps(Graphics g)
    {
        foreach (PowerUp powerUp in _powerUps)
        {
            Color color = powerUp.Kind switch
            {
                PowerUpKind.Heal => Color.LimeGreen,
                PowerUpKind.RapidFire => Color.Gold,
                PowerUpKind.Bomb => Color.DeepSkyBlue,
                PowerUpKind.SpreadShot => Color.HotPink,
                PowerUpKind.PierceShot => Color.MediumPurple,
                _ => Color.Cyan
            };

            using Pen pen = new(color, 3f);
            using SolidBrush glow = new(Color.FromArgb(40, color));

            g.FillEllipse(glow, powerUp.Position.X - 22, powerUp.Position.Y - 22, 44, 44);
            g.DrawEllipse(pen, powerUp.Position.X - powerUp.Radius, powerUp.Position.Y - powerUp.Radius, powerUp.Radius * 2, powerUp.Radius * 2);

            using Font font = new("Segoe UI", 10, FontStyle.Bold);
            using SolidBrush textBrush = new(color);

            string label = powerUp.Kind switch
            {
                PowerUpKind.Heal => "H",
                PowerUpKind.RapidFire => "R",
                PowerUpKind.Bomb => "B",
                PowerUpKind.SpreadShot => "S",
                PowerUpKind.PierceShot => "P",
                _ => "C"
            };

            SizeF size = g.MeasureString(label, font);
            g.DrawString(label, font, textBrush, powerUp.Position.X - size.Width / 2f, powerUp.Position.Y - size.Height / 2f);
        }
    }

    private void DrawCrosshair(Graphics g)
    {
        using Pen pen = new(Color.White, 2f);
        g.DrawEllipse(pen, _mousePosition.X - 10, _mousePosition.Y - 10, 20, 20);
        g.DrawLine(pen, _mousePosition.X - 16, _mousePosition.Y, _mousePosition.X - 6, _mousePosition.Y);
        g.DrawLine(pen, _mousePosition.X + 6, _mousePosition.Y, _mousePosition.X + 16, _mousePosition.Y);
        g.DrawLine(pen, _mousePosition.X, _mousePosition.Y - 16, _mousePosition.X, _mousePosition.Y - 6);
        g.DrawLine(pen, _mousePosition.X, _mousePosition.Y + 6, _mousePosition.X, _mousePosition.Y + 16);
    }

    private void DrawHud(Graphics g)
    {
        using Font font = new("Segoe UI", 12, FontStyle.Bold);
        using Font smallFont = new("Segoe UI", 10, FontStyle.Regular);

        g.DrawString($"Score: {_score}", font, Brushes.White, 18, 16);
        g.DrawString($"Wave: {_wave}", font, Brushes.White, 18, 44);
        g.DrawString($"Time: {_survivalTime:0.0}s", font, Brushes.White, 18, 72);
        g.DrawString($"Kills: {_kills}", font, Brushes.White, 18, 100);

        float barX = 18;
        float barY = 136;
        float barWidth = 260;
        float barHeight = 18;

        using SolidBrush bgBrush = new(Color.FromArgb(40, 255, 255, 255));
        using SolidBrush healthBrush = new(Color.FromArgb(0, 255, 170));
        g.FillRectangle(bgBrush, barX, barY, barWidth, barHeight);

        float currentWidth = barWidth * (_player.Health / _player.MaxHealth);
        g.FillRectangle(healthBrush, barX, barY, currentWidth, barHeight);
        g.DrawRectangle(Pens.White, barX, barY, barWidth, barHeight);
        g.DrawString($"HP {_player.Health:0}/{_player.MaxHealth:0}", smallFont, Brushes.White, barX + 6, barY + 1);

        int rightX = WorldWidth - 255;
        g.DrawString(_player.RapidFireTimer > 0f ? $"Rapid Fire: {_player.RapidFireTimer:0.0}s" : "Rapid Fire: OFF", font, Brushes.Gold, rightX, 16);
        g.DrawString(_player.SpreadShotTimer > 0f ? $"Spread: {_player.SpreadShotTimer:0.0}s" : $"Spread: +{_player.PermanentExtraBullets} permanent", font, Brushes.HotPink, rightX, 44);
        g.DrawString(_player.PierceShotTimer > 0f ? $"Pierce: {_player.PierceShotTimer:0.0}s" : "Pierce: OFF", font, Brushes.MediumPurple, rightX, 72);
        g.DrawString(_player.DashCooldown > 0f ? $"Dash: {_player.DashCooldown:0.0}s" : "Dash: Ready", font, Brushes.Cyan, rightX, 100);
        g.DrawString($"Shield: {_player.ShieldHits}", font, Brushes.Cyan, rightX, 128);

        string powerUps = "Power-ups: H heal, R rapid, B bomb, S spread, P pierce, C shield";
        g.DrawString(powerUps, smallFont, Brushes.WhiteSmoke, 18, WorldHeight - 30);

        if (_notificationTimer > 0f && !string.IsNullOrWhiteSpace(_notification))
        {
            SizeF size = g.MeasureString(_notification, font);
            float x = (WorldWidth - size.Width) / 2f;
            using SolidBrush panel = new(Color.FromArgb(110, 0, 0, 0));
            g.FillRectangle(panel, x - 10, 16, size.Width + 20, size.Height + 8);
            g.DrawString(_notification, font, Brushes.Cyan, x, 20);
        }
    }

    private void DrawPauseOverlay(Graphics g)
    {
        using SolidBrush overlayBrush = new(Color.FromArgb(160, 0, 0, 0));
        g.FillRectangle(overlayBrush, ClientRectangle);

        using Font titleFont = new("Segoe UI", 26, FontStyle.Bold);
        using Font textFont = new("Segoe UI", 14, FontStyle.Bold);

        DrawCenteredText(g, "PAUSED", titleFont, Brushes.Cyan, 220);
        DrawCenteredText(g, "Press P or Esc to resume", textFont, Brushes.White, 300);
        DrawCenteredText(g, "Press R to restart the run", textFont, Brushes.White, 338);
    }

    private void DrawGameOverOverlay(Graphics g)
    {
        using SolidBrush overlayBrush = new(Color.FromArgb(170, 0, 0, 0));
        g.FillRectangle(overlayBrush, ClientRectangle);

        using Font titleFont = new("Segoe UI", 30, FontStyle.Bold);
        using Font textFont = new("Segoe UI", 14, FontStyle.Bold);

        DrawCenteredText(g, "YOU GOT MULCHED", titleFont, Brushes.HotPink, 200);
        DrawCenteredText(g, $"Final Score: {_score}", textFont, Brushes.White, 284);
        DrawCenteredText(g, $"Survived: {_survivalTime:0.0} seconds", textFont, Brushes.White, 320);
        DrawCenteredText(g, $"Wave Reached: {_wave}", textFont, Brushes.White, 356);
        DrawCenteredText(g, $"Best Score: {_highScore.BestScore}", textFont, Brushes.Gold, 392);
        DrawCenteredText(g, "Press R to restart", textFont, Brushes.Cyan, 446);
    }

    private void DrawCenteredText(Graphics g, string text, Font font, Brush brush, int y)
    {
        SizeF size = g.MeasureString(text, font);
        float x = (WorldWidth - size.Width) / 2f;
        g.DrawString(text, font, brush, x, y);
    }
}
