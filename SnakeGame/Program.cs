using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

enum Direction { Up, Down, Left, Right }

class SnakeGame
{
    // Board dimensions (inner play area, excluding walls)
    const int Width = 78;
    const int Height = 26;
    const int OffsetX = 1;
    const int OffsetY = 2; // row 0 = scores, row 1 = top wall

    const string HighScoreFile = "highscore.txt";

    // Snake
    LinkedList<(int x, int y)> snake = new();
    Direction dir = Direction.Right;
    Direction nextDir = Direction.Right;

    // Food
    (int x, int y) food;

    // State
    int score = 0;
    int highScore = 0;
    bool paused = false;
    bool gameOver = false;
    bool victory = false;

    // Rendering buffer
    char[,] buffer = new char[Height + 2, Width + 2];
    ConsoleColor[,] colorBuffer = new ConsoleColor[Height + 2, Width + 2];
    char[,] prevBuffer = new char[Height + 2, Width + 2];
    ConsoleColor[,] prevColorBuffer = new ConsoleColor[Height + 2, Width + 2];

    Random rng = new Random();

    static void Main()
    {
        Console.Title = "Snake Game - C# Console";
        Console.SetWindowSize(80, 30);
        Console.SetBufferSize(80, 30);
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
        score = 0;
        paused = false;
        gameOver = false;
        victory = false;
        dir = Direction.Right;
        nextDir = Direction.Right;

        // Start snake in the middle, 3 segments
        // Head at midX (rightmost), tail extends left — moving Right is safe
        int midX = Width / 2;
        int midY = Height / 2;
        snake.AddLast((midX, midY));       // head
        snake.AddLast((midX - 1, midY));   // body
        snake.AddLast((midX - 2, midY));   // tail

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
                int delay = Math.Max(50, 150 - (score / 10 * 3));
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
            Direction.Up    => (head.x, head.y - 1),
            Direction.Down  => (head.x, head.y + 1),
            Direction.Left  => (head.x - 1, head.y),
            Direction.Right => (head.x + 1, head.y),
            _ => head
        };

        // Wall collision
        if (nx < 0 || nx >= Width || ny < 0 || ny >= Height)
        { gameOver = true; return; }

        // Self collision (skip head itself, check body only)
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
            if (score > highScore)
            {
                highScore = score;
                SaveHighScore(highScore);
            }
            // Check for victory (board full)
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

    // ── Rendering ────────────────────────────────────────────────────────────

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

        // Clear buffer
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                buffer[r, c] = ' ';
                colorBuffer[r, c] = ConsoleColor.Black;
            }

        // Walls
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

        // Food
        SetCell(food.y + 1, food.x + 1, '●', ConsoleColor.Red);

        // Snake body (draw first so head overwrites)
        bool first = true;
        foreach (var seg in snake)
        {
            if (first) { first = false; continue; }
            SetCell(seg.y + 1, seg.x + 1, '■', ConsoleColor.DarkGreen);
        }

        // Snake head
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
        string scoreStr = $" Score: {score}";
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
        // Force full re-render
        ClearBuffers();
        Render();
    }

    void RenderGameOver()
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Yellow;

        string title  = victory ? "*** YOU WIN! ***" : "*** GAME OVER ***";
        string s1     = $"Final Score : {score}";
        string s2     = $"High Score  : {highScore}";
        string s3     = "Press Space or Enter to play again";
        string s4     = "Press Escape to quit";

        int midRow = 12;
        WriteCentered(midRow,     title,  ConsoleColor.Yellow);
        WriteCentered(midRow + 2, s1,     ConsoleColor.White);
        WriteCentered(midRow + 3, s2,     ConsoleColor.White);
        WriteCentered(midRow + 5, s3,     ConsoleColor.Cyan);
        WriteCentered(midRow + 6, s4,     ConsoleColor.DarkCyan);
        Console.ResetColor();
    }

    void WriteCentered(int row, string text, ConsoleColor color)
    {
        int col = Math.Max(0, (80 - text.Length) / 2);
        Console.SetCursorPosition(col, row);
        Console.ForegroundColor = color;
        Console.Write(text);
    }

    // ── Persistence ──────────────────────────────────────────────────────────

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
