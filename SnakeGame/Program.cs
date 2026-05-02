using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

enum Direction { Up, Down, Left, Right }

class SnakeGame
{
    const int Width = 78;
    const int Height = 26;
    const int OffsetX = 1;
    const int OffsetY = 2;

    const string HighScoreFile = "highscore.txt";

    LinkedList<(int x, int y)> snake = new();
    List<(int x, int y)> obstacles = new();

    Direction dir = Direction.Right;
    Direction nextDir = Direction.Right;

    (int x, int y) food;

    int score = 0;
    int highScore = 0;
    int level = 1; // ✅ NEW

    bool paused = false;
    bool gameOver = false;
    bool victory = false;

    char[,] buffer = new char[Height + 2, Width + 2];
    ConsoleColor[,] colorBuffer = new ConsoleColor[Height + 2, Width + 2];
    char[,] prevBuffer = new char[Height + 2, Width + 2];
    ConsoleColor[,] prevColorBuffer = new ConsoleColor[Height + 2, Width + 2];

    Random rng = new Random();

    static void Main()
    {
        Console.Title = "Snake Game - C# Console";

        try
        {
            Console.SetWindowSize(80, 30);
            Console.SetBufferSize(80, 30);
        }
        catch { }

        Console.CursorVisible = false;

        while (true)
        {
            var game = new SnakeGame();
            game.Run();
            if (!game.WaitForRestart())
                break;
        }

        Console.CursorVisible = true;
        Console.Clear();
    }

    public SnakeGame()
    {
        highScore = LoadHighScore();
        InitGame();
    }

    void InitGame()
    {
        snake.Clear();
        obstacles.Clear();

        score = 0;
        level = 1; // ✅ RESET LEVEL
        paused = false;
        gameOver = false;
        victory = false;

        dir = Direction.Right;
        nextDir = Direction.Right;

        int midX = Width / 2;
        int midY = Height / 2;

        snake.AddLast((midX, midY));
        snake.AddLast((midX - 1, midY));
        snake.AddLast((midX - 2, midY));

        // Starting obstacles
        obstacles.Add((10, 5));
        obstacles.Add((20, 10));
        obstacles.Add((30, 15));

        SpawnFood();
        ClearBuffers();
    }

    void ClearBuffers()
    {
        for (int r = 0; r < Height + 2; r++)
            for (int c = 0; c < Width + 2; c++)
            {
                prevBuffer[r, c] = '\0';
                prevColorBuffer[r, c] = ConsoleColor.Black;
            }
    }

    public void Run()
    {
        Console.Clear();
        while (!gameOver && !victory)
        {
            HandleInput();
            if (!paused)
            {
                Update();
                Render();

                // ✅ SPEED BASED ON LEVEL
                int delay = Math.Max(50, 150 - (level * 10));
                Thread.Sleep(delay);
            }
            else
            {
                DrawPause();
                Thread.Sleep(50);
            }
        }
        RenderGameOver();
    }

    public bool WaitForRestart()
    {
        while (true)
        {
            if (Console.KeyAvailable)
            {
                var k = Console.ReadKey(true).Key;
                if (k == ConsoleKey.Spacebar || k == ConsoleKey.Enter)
                    return true;
                if (k == ConsoleKey.Escape)
                    return false;
            }
            Thread.Sleep(50);
        }
    }

    void HandleInput()
    {
        while (Console.KeyAvailable)
        {
            var key = Console.ReadKey(true).Key;
            switch (key)
            {
                case ConsoleKey.UpArrow:
                    if (dir != Direction.Down) nextDir = Direction.Up; break;
                case ConsoleKey.DownArrow:
                    if (dir != Direction.Up) nextDir = Direction.Down; break;
                case ConsoleKey.LeftArrow:
                    if (dir != Direction.Right) nextDir = Direction.Left; break;
                case ConsoleKey.RightArrow:
                    if (dir != Direction.Left) nextDir = Direction.Right; break;
                case ConsoleKey.P:
                    paused = !paused;
                    if (!paused) ClearPause();
                    break;
                case ConsoleKey.Escape:
                    gameOver = true; break;
            }
        }
    }

    void Update()
    {
        dir = nextDir;
        var head = snake.First!.Value;

        (int nx, int ny) = dir switch
        {
            Direction.Up => (head.x, head.y - 1),
            Direction.Down => (head.x, head.y + 1),
            Direction.Left => (head.x - 1, head.y),
            Direction.Right => (head.x + 1, head.y),
            _ => head
        };

        if (nx < 0 || nx >= Width || ny < 0 || ny >= Height)
        { gameOver = true; return; }

        foreach (var obs in obstacles)
        {
            if (nx == obs.x && ny == obs.y)
            { gameOver = true; return; }
        }

        bool skipFirst = true;
        foreach (var seg in snake)
        {
            if (skipFirst) { skipFirst = false; continue; }
            if (seg.x == nx && seg.y == ny)
            { gameOver = true; return; }
        }

        snake.AddFirst((nx, ny));

        if (nx == food.x && ny == food.y)
        {
            score += 10;

            // ✅ LEVEL SYSTEM
            int newLevel = (score / 50) + 1;
            if (newLevel > level)
                level = newLevel;

            // Obstacles scaling
            if (score % 30 == 0)
            {
                var newObs = (rng.Next(0, Width), rng.Next(0, Height));
                if (!snake.Contains(newObs) && newObs != food)
                    obstacles.Add(newObs);
            }

            if (score > highScore)
            {
                highScore = score;
                SaveHighScore(highScore);
            }

            if (snake.Count >= Width * Height)
            { victory = true; return; }

            SpawnFood();
        }
        else
        {
            snake.RemoveLast();
        }
    }

    void SpawnFood()
    {
        var occupied = new HashSet<(int, int)>(snake);
        var empty = new List<(int, int)>();

        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                if (!occupied.Contains((x, y)))
                    empty.Add((x, y));

        if (empty.Count == 0) { victory = true; return; }
        food = empty[rng.Next(empty.Count)];
    }

    void Render()
    {
        BuildBuffer();
        FlushBuffer();
        DrawScoreBar();
    }

    void BuildBuffer()
    {
        int rows = Height + 2;
        int cols = Width + 2;

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                buffer[r, c] = ' ';
                colorBuffer[r, c] = ConsoleColor.Black;
            }

        for (int c = 0; c < cols; c++)
        {
            SetCell(0, c, '█', ConsoleColor.DarkGray);
            SetCell(rows - 1, c, '█', ConsoleColor.DarkGray);
        }

        for (int r = 0; r < rows; r++)
        {
            SetCell(r, 0, '█', ConsoleColor.DarkGray);
            SetCell(r, cols - 1, '█', ConsoleColor.DarkGray);
        }

        SetCell(food.y + 1, food.x + 1, '●', ConsoleColor.Red);

        foreach (var obs in obstacles)
            SetCell(obs.y + 1, obs.x + 1, '#', ConsoleColor.DarkRed);

        bool first = true;
        foreach (var seg in snake)
        {
            if (first) { first = false; continue; }
            SetCell(seg.y + 1, seg.x + 1, '■', ConsoleColor.DarkGreen);
        }

        var h = snake.First!.Value;
        SetCell(h.y + 1, h.x + 1, '@', ConsoleColor.Green);
    }

    void SetCell(int row, int col, char ch, ConsoleColor color)
    {
        buffer[row, col] = ch;
        colorBuffer[row, col] = color;
    }

    void FlushBuffer()
    {
        int rows = Height + 2;
        int cols = Width + 2;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (buffer[r, c] == prevBuffer[r, c] &&
                    colorBuffer[r, c] == prevColorBuffer[r, c])
                    continue;

                Console.SetCursorPosition(c, r + OffsetY - 1);
                Console.ForegroundColor = colorBuffer[r, c];
                Console.Write(buffer[r, c]);

                prevBuffer[r, c] = buffer[r, c];
                prevColorBuffer[r, c] = colorBuffer[r, c];
            }
        }
        Console.ResetColor();
    }

    void DrawScoreBar()
    {
        Console.SetCursorPosition(0, 0);
        Console.ForegroundColor = ConsoleColor.White;

        string scoreStr = $" Score: {score} | Level: {level}";
        string hiStr = $"High Score: {highScore} ";

        Console.Write(scoreStr.PadRight(80 - hiStr.Length));
        Console.Write(hiStr);
        Console.ResetColor();
    }

    void DrawPause()
    {
        string msg = "  *** PAUSED — Press P to resume ***  ";
        int col = (80 - msg.Length) / 2;
        int row = (30 - 1) / 2;

        Console.SetCursorPosition(col, row);
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write(msg);
        Console.ResetColor();
    }

    void ClearPause()
    {
        string blank = new string(' ', 40);
        int col = (80 - 38) / 2;
        int row = (30 - 1) / 2;

        Console.SetCursorPosition(col, row);
        Console.Write(blank);

        ClearBuffers();
        Render();
    }

    void RenderGameOver()
    {
        Console.Clear();

        string title = victory ? "*** YOU WIN! ***" : "*** GAME OVER ***";

        WriteCentered(12, title, ConsoleColor.Yellow);
        WriteCentered(14, $"Final Score : {score}", ConsoleColor.White);
        WriteCentered(15, $"High Score  : {highScore}", ConsoleColor.White);
        WriteCentered(17, "Press Space or Enter to play again", ConsoleColor.Cyan);
        WriteCentered(18, "Press Escape to quit", ConsoleColor.DarkCyan);
    }

    void WriteCentered(int row, string text, ConsoleColor color)
    {
        int col = Math.Max(0, (80 - text.Length) / 2);
        Console.SetCursorPosition(col, row);
        Console.ForegroundColor = color;
        Console.Write(text);
    }

    static int LoadHighScore()
    {
        try
        {
            if (File.Exists(HighScoreFile))
                return int.Parse(File.ReadAllText(HighScoreFile).Trim());
        }
        catch { }
        return 0;
    }

    static void SaveHighScore(int score)
    {
        try { File.WriteAllText(HighScoreFile, score.ToString()); }
        catch { }
    }
}