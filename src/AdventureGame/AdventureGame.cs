using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace AdventureGame;

public class AdventureGame
{
    public readonly string GO_NORTH = "W";
    public readonly string GO_SOUTH = "S";
    public readonly string GO_EAST = "D";
    public readonly string GO_WEST = "A";
    public readonly string GET_LAMP = "L";
    public readonly string GET_KEY = "K";
    public readonly string OPEN_CHEST = "O";
    public readonly string QUIT = "Q";

    private Adventurer adventurer;
    private Room[,] dungeon;
    private int aRow;
    private int aCol;
    private bool isChestOpen;
    private bool hasPlayerQuit;
    private bool isAdventureAlive;
    private string lastDirection;
    
    // New fields for enhanced mechanics
    private int exitRow;
    private int exitCol;
    private int grueRow;
    private int grueCol;
    private bool isGruePursuing;
    private bool hasExited;

    public AdventureGame()
    {

    }

    public void Start()
    {
        Init();

        ShowGameStartScreen();

        string input;

        do
        {
            ShowScene();
            
            // Check if grue catches player at the start of each turn
            if (isGruePursuing && grueRow == aRow && grueCol == aCol)
            {
                Console.WriteLine("\nThe Grue catches you in the darkness! You are devoured!");
                isAdventureAlive = false;
                break;
            }

            do
            {
                ShowInputOptions();

                input = GetInput();
            }
            while (!IsValidInput(input));

            ProcessInput(input);

            UpdateGameState();
            
            // Check if player reached exit
            if (!hasExited && aRow == exitRow && aCol == exitCol && isChestOpen)
            {
                Console.WriteLine("\n*** You have found the dungeon exit and escaped! ***");
                Console.WriteLine("Congratulations! You are free!");
                hasExited = true;
                break;
            }
        }
        while (!IsGameOver());

        ShowGameOverScreen();
    }

    private void CreateDefaultDungeon()
    {
        int rows = 8;
        int cols = 8;
        dungeon = new Room[rows, cols];
        
        // Set default positions
        exitRow = 0;
        exitCol = 7;
        aRow = 1;
        aCol = 0;
        grueRow = 7;
        grueCol = 7;
        
        // Create all rooms with connections
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                dungeon[i, j] = new Room();
                dungeon[i, j].SetDescription($"Room ({i},{j})");
                dungeon[i, j].SetLit(i == 0 || i == 1 || j == 0); // Some rooms are lit
                
                // Add exits with bounds checking
                dungeon[i, j].SetNorth(i > 0);
                dungeon[i, j].SetSouth(i < rows - 1);
                dungeon[i, j].SetEast(j < cols - 1);
                dungeon[i, j].SetWest(j > 0);
            }
        }
        
        // Place items
        dungeon[0, 1].SetLamp(true);  // Lamp
        dungeon[2, 5].SetKey(true);   // Key
        dungeon[5, 5].SetChest(true); // Chest
        
        // Special descriptions
        dungeon[0, 0].SetDescription("Entrance Hall - A grand entrance");
        dungeon[1, 0].SetDescription("Starting Room - Where you begin your journey");
        dungeon[0, 7].SetDescription("Exit Chamber - The way out!");
        dungeon[5, 5].SetDescription("Treasure Room - A chest sits in the center");
        dungeon[7, 7].SetDescription("Grue Lair - You sense danger here");
        
        Console.WriteLine("Default dungeon created successfully!");
    }

    private void ValidateCoordinates(int rows, int cols, int row, int col, string coordName)
    {
        if (row < 0 || row >= rows || col < 0 || col >= cols)
        {
            throw new InvalidDataException($"{coordName} at ({row}, {col}) is outside dungeon bounds ({rows}x{cols})");
        }
    }

    private void LoadDungeonFromFile(string filename)
    {
        // If file doesn't exist, create a default dungeon
        if (!File.Exists(filename))
        {
            Console.WriteLine($"Warning: '{filename}' not found. Creating default dungeon...");
            CreateDefaultDungeon();
            return;
        }

        string[] lines = File.ReadAllLines(filename);
        int rows = 0, cols = 0;
        
        // Initialize default values
        exitRow = -1;
        exitCol = -1;
        aRow = -1;
        aCol = -1;
        int lampRow = -1, lampCol = -1;
        int keyRow = -1, keyCol = -1;
        int chestRow = -1, chestCol = -1;
        grueRow = -1;
        grueCol = -1;
        
        // Temporary room storage
        var tempRooms = new Dictionary<string, (bool lit, string desc, bool n, bool s, bool e, bool w)>();

        foreach (string line in lines)
        {
            string trimmedLine = line.Trim();
            
            // Skip comments and empty lines
            if (trimmedLine.StartsWith("#") || string.IsNullOrWhiteSpace(trimmedLine))
                continue;

            string[] parts = Regex.Split(trimmedLine, @"\s+(?=(?:[^""]*""[^""]*"")*[^""]*$)");
            
            if (parts.Length < 2) continue;

            switch (parts[0].ToUpper())
            {
                case "DIMENSIONS":
                    rows = int.Parse(parts[1]);
                    cols = int.Parse(parts[2]);
                    
                    // Add bounds checking
                    if (rows <= 0 || cols <= 0)
                        throw new InvalidDataException($"Invalid dimensions: {rows}x{cols}");
                    if (rows > 100 || cols > 100)  // Sanity check
                        throw new InvalidDataException($"Dimensions too large: {rows}x{cols}");
                    
                    dungeon = new Room[rows, cols];
                    break;
                    
                case "EXIT":
                    exitRow = int.Parse(parts[1]);
                    exitCol = int.Parse(parts[2]);
                    break;
                    
                case "START":
                    aRow = int.Parse(parts[1]);
                    aCol = int.Parse(parts[2]);
                    break;
                    
                case "LAMP":
                    lampRow = int.Parse(parts[1]);
                    lampCol = int.Parse(parts[2]);
                    break;
                    
                case "KEY":
                    keyRow = int.Parse(parts[1]);
                    keyCol = int.Parse(parts[2]);
                    break;
                    
                case "CHEST":
                    chestRow = int.Parse(parts[1]);
                    chestCol = int.Parse(parts[2]);
                    break;
                    
                case "GRUE":
                    grueRow = int.Parse(parts[1]);
                    grueCol = int.Parse(parts[2]);
                    break;
                    
                case "ROOM":
                    int row = int.Parse(parts[1]);
                    int col = int.Parse(parts[2]);
                    bool lit = parts[3] == "1";
                    string desc = parts[4].Trim('"');
                    bool north = parts[5] == "1";
                    bool south = parts[6] == "1";
                    bool east = parts[7] == "1";
                    bool west = parts[8] == "1";
                    
                    tempRooms[$"{row},{col}"] = (lit, desc, north, south, east, west);
                    break;
            }
        }

        // Validate required coordinates
        if (rows == 0 || cols == 0) throw new InvalidDataException("DIMENSIONS not specified");
        if (exitRow == -1) throw new InvalidDataException("EXIT not specified");
        if (aRow == -1) throw new InvalidDataException("START not specified");
        if (lampRow == -1) throw new InvalidDataException("LAMP not specified");
        if (keyRow == -1) throw new InvalidDataException("KEY not specified");
        if (chestRow == -1) throw new InvalidDataException("CHEST not specified");
        if (grueRow == -1) throw new InvalidDataException("GRUE not specified");

        // Validate all coordinates are within bounds
        ValidateCoordinates(rows, cols, exitRow, exitCol, "EXIT");
        ValidateCoordinates(rows, cols, aRow, aCol, "START");
        ValidateCoordinates(rows, cols, lampRow, lampCol, "LAMP");
        ValidateCoordinates(rows, cols, keyRow, keyCol, "KEY");
        ValidateCoordinates(rows, cols, chestRow, chestCol, "CHEST");
        ValidateCoordinates(rows, cols, grueRow, grueCol, "GRUE");

        // Initialize all rooms
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                dungeon[i, j] = new Room();
                
                if (tempRooms.TryGetValue($"{i},{j}", out var roomData))
                {
                    dungeon[i, j].SetLit(roomData.lit);
                    dungeon[i, j].SetDescription(roomData.desc);
                    dungeon[i, j].SetNorth(roomData.n);
                    dungeon[i, j].SetSouth(roomData.s);
                    dungeon[i, j].SetEast(roomData.e);
                    dungeon[i, j].SetWest(roomData.w);
                }
                else
                {
                    // Default room if not specified
                    dungeon[i, j].SetDescription($"Room ({i},{j})");
                }
            }
        }

        // Place items
        dungeon[lampRow, lampCol].SetLamp(true);
        dungeon[keyRow, keyCol].SetKey(true);
        dungeon[chestRow, chestCol].SetChest(true);
        
        Console.WriteLine($"Dungeon loaded successfully! Size: {rows}x{cols}");
    }

    private void Init()
    {
        adventurer = new Adventurer();
        
        // Load dungeon from file
        try
        {
            // Try to find the file in multiple locations
            string[] pathsToTry = {
                "dungeon.dng",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dungeon.dng"),
                Path.Combine(Directory.GetCurrentDirectory(), "dungeon.dng"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "dungeon.dng")
            };
            
            string foundPath = null;
            foreach (string path in pathsToTry)
            {
                if (File.Exists(path))
                {
                    foundPath = path;
                    break;
                }
            }
            
            if (foundPath != null)
                LoadDungeonFromFile(foundPath);
            else
                LoadDungeonFromFile("dungeon.dng"); // This will trigger default dungeon creation
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading dungeon: {ex.Message}");
            Console.WriteLine("Creating default dungeon instead...");
            CreateDefaultDungeon();
        }

        isChestOpen = false;
        hasPlayerQuit = false;
        isAdventureAlive = true;
        isGruePursuing = false;
        hasExited = false;
        lastDirection = string.Empty;
    }

    private void ShowGameStartScreen()
    {
        Console.WriteLine("========================================");
        Console.WriteLine("      WELCOME TO ADVENTURE GAME!        ");
        Console.WriteLine("========================================");
        Console.WriteLine("You are trapped in a dark dungeon.");
        Console.WriteLine("Find the lamp, get the key, open the chest,");
        Console.WriteLine("and escape through the exit!");
        Console.WriteLine("But beware... the Grue hunts in darkness!\n");
    }

    private void MoveGrue()
    {
        if (!isGruePursuing) return;
        
        // Simple pathfinding - move towards player using Manhattan distance priority
        int bestRow = grueRow;
        int bestCol = grueCol;
        int bestDist = Math.Abs(grueRow - aRow) + Math.Abs(grueCol - aCol);
        
        // Check all four directions
        int[,] directions = { { -1, 0 }, { 1, 0 }, { 0, -1 }, { 0, 1 } };
        
        for (int i = 0; i < 4; i++)
        {
            int newRow = grueRow + directions[i, 0];
            int newCol = grueCol + directions[i, 1];
            
            if (newRow >= 0 && newRow < dungeon.GetLength(0) && 
                newCol >= 0 && newCol < dungeon.GetLength(1))
            {
                int dist = Math.Abs(newRow - aRow) + Math.Abs(newCol - aCol);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestRow = newRow;
                    bestCol = newCol;
                }
            }
        }
        
        grueRow = bestRow;
        grueCol = bestCol;
        
        Console.WriteLine($"\nYou hear skittering sounds... The Grue is getting closer!");
    }

    private void ShowScene()
    {
        // Add bounds checking
        if (aRow < 0 || aRow >= dungeon.GetLength(0) || aCol < 0 || aCol >= dungeon.GetLength(1))
        {
            Console.WriteLine($"\nERROR: Invalid position ({aRow}, {aCol})! Resetting to start...");
            aRow = 1;
            aCol = 0;
            return;
        }
        
        Console.WriteLine($"\n[Position: ({aRow}, {aCol})]");
        
        var r = dungeon[aRow, aCol];

        if (adventurer.HasLamp() || r.IsLit())
        {
            Console.WriteLine(r.GetDescription());
            
            // Show exits
            string exits = "";
            if (r.HasNorth()) exits += "North ";
            if (r.HasSouth()) exits += "South ";
            if (r.HasEast()) exits += "East ";
            if (r.HasWest()) exits += "West ";
            if (!string.IsNullOrEmpty(exits))
                Console.WriteLine($"Exits: {exits}");
        }
        else
        {
            Console.WriteLine("This room is pitch black!");
        }
        
        // Show if items are present (only if room is lit or player has lamp)
        if (adventurer.HasLamp() || r.IsLit())
        {
            if (r.HasLamp() && !adventurer.HasLamp())
                Console.WriteLine("You see a lamp here.");
            if (r.HasKey() && !adventurer.HasKey())
                Console.WriteLine("You see a key here.");
            if (r.HasChest() && !isChestOpen)
                Console.WriteLine("There is a chest here.");
            if (aRow == exitRow && aCol == exitCol && isChestOpen)
                Console.WriteLine("*** THE DUNGEON EXIT IS HERE! ***");
        }
        
        if (isGruePursuing)
        {
            Console.WriteLine("You feel a presence hunting you...");
        }
    }

    private void ShowInputOptions()
    {
        string options = ""
        + $"GO NORTH [{GO_NORTH}] | GO EAST [{GO_EAST}] | GET LAMP [{GET_LAMP}] | OPEN CHEST [{OPEN_CHEST}]\n"
        + $"GO SOUTH [{GO_SOUTH}] | GO WEST [{GO_WEST}] | GET KEY  [{GET_KEY}] | QUIT       [{QUIT}]\n"
        + $"> ";

        Console.Write(options);
    }

    private string GetInput()
    {
        return Console.ReadLine()!.ToUpper();
    }

    private bool IsValidInput(string input)
    {
        string[] validInputs = { GO_NORTH, GO_SOUTH, GO_EAST, GO_WEST, GET_LAMP, GET_KEY, OPEN_CHEST, QUIT };

        if (!validInputs.Contains(input))
        {
            Console.WriteLine("ERROR: Invalid input. Please try again.");
            return false;
        }

        return true;
    }

    private void ProcessInput(string input)
    {
        Room r = dungeon[aRow, aCol];

        if (!adventurer.HasLamp() && !r.IsLit() && input != lastDirection)
        {
            Console.WriteLine("\nThe Grue attacks from the darkness! You are devoured!");
            isAdventureAlive = false;
        }
        else if (input == GO_NORTH)
        {
            GoNorth(r);
            MoveGrue();
        }
        else if (input == GO_SOUTH)
        {
            GoSouth(r);
            MoveGrue();
        }
        else if (input == GO_EAST)
        {
            GoEast(r);
            MoveGrue();
        }
        else if (input == GO_WEST)
        {
            GoWest(r);
            MoveGrue();
        }
        else if (input == GET_LAMP)
        {
            GetLamp(r);
        }
        else if (input == GET_KEY)
        {
            GetKey(r);
        }
        else if (input == OPEN_CHEST)
        {
            OpenChest(r);
        }
        else // if(input == QUIT)
        {
            Quit();
        }
    }

    private void UpdateGameState()
    {
        // Additional state updates can go here
    }

    private bool IsGameOver()
    {
        return hasExited || hasPlayerQuit || !isAdventureAlive;
    }

    private void ShowGameOverScreen()
    {
        Console.WriteLine("\n========================================");
        if (hasExited)
        {
            Console.WriteLine("      VICTORY! YOU ESCAPED!         ");
            Console.WriteLine("    Thanks for playing!             ");
        }
        else if (isChestOpen)
        {
            Console.WriteLine("      YOU OPENED THE CHEST!          ");
            Console.WriteLine("But the Grue is now hunting you...   ");
            Console.WriteLine("Find the exit before it's too late!");
        }
        else if (!isAdventureAlive)
        {
            Console.WriteLine("      GAME OVER - YOU DIED!          ");
            Console.WriteLine("    The Grue got you...             ");
        }
        else if (hasPlayerQuit)
        {
            Console.WriteLine("      GAME OVER - YOU QUIT           ");
        }
        Console.WriteLine("========================================");
        
        // Pause before exiting
        Console.WriteLine("\nPress Enter to exit...");
        Console.ReadLine();
    }

    private void GoNorth(Room r)
    {
        if (r.HasNorth() && aRow - 1 >= 0)
        {
            aRow -= 1;
            lastDirection = GO_SOUTH;
            Console.WriteLine("You move north.");
        }
        else
        {
            if (aRow - 1 < 0)
                Console.WriteLine("Cannot go north - edge of dungeon!");
            else
                Console.WriteLine("You cannot go north!\a");
        }
    }

    private void GoSouth(Room r)
    {
        if (r.HasSouth() && aRow + 1 < dungeon.GetLength(0))
        {
            aRow += 1;
            lastDirection = GO_NORTH;
            Console.WriteLine("You move south.");
        }
        else
        {
            if (aRow + 1 >= dungeon.GetLength(0))
                Console.WriteLine("Cannot go south - edge of dungeon!");
            else
                Console.WriteLine("You cannot go south!\a");
        }
    }

    private void GoEast(Room r)
    {
        if (r.HasEast() && aCol + 1 < dungeon.GetLength(1))
        {
            aCol += 1;
            lastDirection = GO_WEST;
            Console.WriteLine("You move east.");
        }
        else
        {
            if (aCol + 1 >= dungeon.GetLength(1))
                Console.WriteLine("Cannot go east - edge of dungeon!");
            else
                Console.WriteLine("You cannot go east!\a");
        }
    }

    private void GoWest(Room r)
    {
        if (r.HasWest() && aCol - 1 >= 0)
        {
            aCol -= 1;
            lastDirection = GO_EAST;
            Console.WriteLine("You move west.");
        }
        else
        {
            if (aCol - 1 < 0)
                Console.WriteLine("Cannot go west - edge of dungeon!");
            else
                Console.WriteLine("You cannot go west!\a");
        }
    }

    private void GetLamp(Room r)
    {
        if (r.HasLamp())
        {
            Console.WriteLine("You got the lamp! The room is now illuminated!");
            adventurer.SetLamp(true);
            r.SetLamp(false);
        }
        else
        {
            Console.WriteLine("There is no lamp in this room.");
        }
    }

    private void GetKey(Room r)
    {
        if (r.HasKey())
        {
            Console.WriteLine("You got the key! It might open something important...");
            adventurer.SetKey(true);
            r.SetKey(false);
        }
        else
        {
            Console.WriteLine("There is no key in this room.");
        }
    }

    private void OpenChest(Room r)
    {
        if (r.HasChest() && !isChestOpen)
        {
            if (adventurer.HasKey())
            {
                Console.WriteLine("\nYou open the chest with the key!");
                Console.WriteLine("Inside, you find ancient treasure...");
                Console.WriteLine("But as you open it, you hear an enraged screech!");
                Console.WriteLine("The Grue now knows where you are and will hunt you!\n");
                isChestOpen = true;
                isGruePursuing = true;
                r.SetChest(false);
            }
            else
            {
                Console.WriteLine("The chest is locked. You need a key!");
            }
        }
        else if (isChestOpen)
        {
            Console.WriteLine("The chest is already open. You took everything valuable.");
        }
        else
        {
            Console.WriteLine("There is no chest in this room.");
        }
    }

    private void Quit()
    {
        Console.WriteLine("You quit the game!");
        hasPlayerQuit = true;
    }
}