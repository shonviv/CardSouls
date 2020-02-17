/*
 * Author: Shon Vivier
 * File Name: Game1.cs
 * Project Name: A1
 * Creation Date: 2/10/2020
 * Modified Date: 2/16/2020
 * Description: Program to emulate classic "Game of Elevens" card game themed around the Dark Souls series.
 * There are no instructions because this is the Dark Souls of card games.
*/

using System;
using System.Linq;
using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;

namespace A1
{
    /// <summary>
    /// An enumeration that represents the states the game can be in.
    /// </summary>
    internal enum GameState
    {
        Menu,
        Initializing,
        Selecting,
        Moving,
        Won,
        Lost,
    }

    /// <summary>
    /// This is the main type for your game.
    /// </summary>
    public class Game1 : Game
    {
        #region Fields

        private static readonly Random Random = new Random();

        // Constants
        private const int Suits = 4;
        private const int CardsPerSuit = 13;
        private const int SlideSounds = 4;
        private const int PileRows = 2;
        private const int PileColumns = 6;
        private const int MinFaceCardValue = 10;
        private const int RequiredSum = 11;

        // The width and height of each individual card
        private int cardWidth;
        private int cardHeight;
        
        // Two seperate mouse states used for getting the exact frame of a button press
        private static MouseState _currentMouseState = Mouse.GetState();
        private static MouseState _lastMouseState = _currentMouseState;

        // Songs
        private Song menuTheme;
        private Song gameTheme;

        // Sound effects
        private readonly SoundEffect[] slideSounds = new SoundEffect[SlideSounds];
        private SoundEffect shuffleSound;
        private SoundEffect buttonSound;
        private SoundEffect lostSound;
        private SoundEffect wonSound;

        // The deck is represented by a stack which allows us to remove and return the top card
        // of the collection. Each card object is a tuple, with the first string value denoting the
        // card suit and the second string value denoting the card value.

        // The deck is a list of tuples, where the first item represents the suit and the second
        // item represents the card value, each starting from 0
        private List<Tuple<int, int>> deck = new List<Tuple<int, int>>();

        // A 2-dimensional array of piles, which are lists of card tuples
        private List<Tuple<int, int>>[,] piles = new List<Tuple<int, int>>[PileRows, PileColumns];

        // Individual piles that are referenced
        private List<Tuple<int, int>> selectedCardPile = new List<Tuple<int, int>>();
        private List<Tuple<int, int>> hoveringCardPile = new List<Tuple<int, int>>();
        
        // Position information used for lerping
        private readonly Vector2[,] pileStartPositions = new Vector2[PileRows, PileColumns];
        private readonly Vector2[,] pileDestinations = new Vector2[PileRows, PileColumns];
        private Vector2[] movingCardDestinations = new Vector2[2];

        // Vector2 instances that store position data about specific cards
        private Vector2 selectedCardPosition;
        private Vector2 deckPosition;

        // Readonly Vector2s used for graphical margins and offsets
        private readonly Vector2 deckTextOffset = new Vector2(-7, 25);
        private readonly Vector2 cardMargins = new Vector2(5, 5);

        // Master sprite sheet
        private Texture2D cardFaceSheet;
        private Texture2D cardBackTexture;
        private Texture2D backgroundTexture;
        private Texture2D menuScreenTexture;
        private Texture2D winScreenTexture;
        private Texture2D lostScreenTexture;

        // Rectangles
        private readonly Rectangle[,] cardRects = new Rectangle[Suits, CardsPerSuit];
        private Rectangle[] movingCardRects = new Rectangle[2];
        private Rectangle backgroundRect;
        private Rectangle gameOverButtonRect = new Rectangle(270, 381, 255, 66);

        // A game state enum that determines what screen to show
        private GameState currentState = GameState.Menu;
        
        private GraphicsDeviceManager graphics;
        private SpriteBatch spriteBatch;

        // The default font used for displaying text data
        private SpriteFont defaultFont;

        // Lerp information
        private readonly float maxLerpTime = 1;
        private float lerpDelta = -1;
        private float lerpCompletion;
        private int pileLerpIndex;
        private int lerpColumn;
        private int lerpRow;

        private bool[,] isPileLerped = new bool[2, 6];
        private bool isPileLerpFinished;
        private bool isFirstCardLerped;

        #endregion // Fields

        #region Constructor
        
        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
        }

        #endregion // Constructor

        #region Methods

        #region Overrides

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            // TODO: Add your initialization logic here
            
            IsMouseVisible = true;

            // Create a new background rectangle with the size of the screen
            backgroundRect = new Rectangle(0, 0, GraphicsDevice.Viewport.Bounds.Width, GraphicsDevice.Viewport.Bounds.Height);
            base.Initialize();
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures
            spriteBatch = new SpriteBatch(GraphicsDevice);

            // Load fonts
            defaultFont = Content.Load<SpriteFont>("Fonts/Default");

            // Load background textures
            backgroundTexture = Content.Load<Texture2D>("Art/Backgrounds/Table");
            winScreenTexture = Content.Load<Texture2D>("Art/Backgrounds/WinScreen");
            lostScreenTexture = Content.Load<Texture2D>("Art/Backgrounds/LostScreen");
            menuScreenTexture = Content.Load<Texture2D>("Art/Backgrounds/MenuScreen");

            // Load card textures
            cardFaceSheet = Content.Load<Texture2D>("Art/Sprites/CardFaces");
            cardBackTexture = Content.Load<Texture2D>("Art/Sprites/CardBack");

            // Load music
            menuTheme = Content.Load<Song>("Music/menutheme");
            gameTheme = Content.Load<Song>("Music/gametheme");

            // Load sound effects
            shuffleSound = Content.Load<SoundEffect>("Sounds/shuffle");
            lostSound = Content.Load<SoundEffect>("Sounds/lost");
            wonSound = Content.Load<SoundEffect>("Sounds/won");
            buttonSound = Content.Load<SoundEffect>("Sounds/button");
            
            // Load all slide sound effects
            for (int i = 0; i < SlideSounds; i++)
            {
                slideSounds[i] = Content.Load<SoundEffect>($"Sounds/slide{i + 1}");
            }
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// game-specific content.
        /// </summary>
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            // Handle music
            if (MediaPlayer.State != MediaState.Playing)
            {
                switch (currentState)
                {
                    case GameState.Menu:
                        MediaPlayer.Volume = 0.1f;
                        MediaPlayer.Play(menuTheme);
                        break;

                    case GameState.Initializing:
                        MediaPlayer.Volume = 0.1f;
                        MediaPlayer.Play(gameTheme);

                        shuffleSound.Play();
                        break;
                }
            }

            switch (currentState)
            {
                case GameState.Menu:
                    UpdateMenuScreen();
                    break;

                case GameState.Initializing:
                    UpdateGameInitialization(gameTime);
                    break;

                case GameState.Selecting:
                    UpdateSelecting();
                    break;

                case GameState.Moving:
                    UpdateMoving(gameTime);
                    break;

                case GameState.Won:
                    UpdateGameEndScreen(gameTime);
                    break;

                case GameState.Lost:
                    UpdateGameEndScreen(gameTime);
                    break;
            }

            base.Update(gameTime);
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            spriteBatch.Begin();
            switch (currentState)
            {
                case GameState.Menu:
                    DrawMenuScreen();
                    break;

                case GameState.Initializing:
                    DrawGameInitialization();
                    break;

                case GameState.Selecting:
                    DrawSelecting();
                    break;

                case GameState.Moving:
                    DrawMoving();
                    break;

                case GameState.Won:
                    DrawWonScreen();
                    break;

                case GameState.Lost:
                    DrawLostScreen();
                    break;
            }

            spriteBatch.End();
            base.Draw(gameTime);
        }

        #endregion // Overrides

        #region Helpers

        /// <summary>
        /// The Pop method removes the top item from the list and returns the removed item.
        /// </summary>
        /// <typeparam name="T">A generic type</typeparam>>
        /// <param name="list">List to pop from <see cref="T:System.Collections.Generic.IList`1"/></param>
        /// <returns></returns>InitializeCards
        private static T Pop<T>(IList<T> list)
        {
            int endIndex = list.Count - 1;
            T r = list[endIndex];
            list.RemoveAt(endIndex);
            return r;
        }

        /// <summary>
        /// The Shuffle method returns a new randomly ordered generic list.
        /// </summary>
        /// <typeparam name="T">A generic type</typeparam>>
        /// <param name="list">List to shuffle</param>
        /// <returns></returns>
        private List<T> Shuffle<T>(IReadOnlyList<T> list)
        {
            return new List<T>(list.OrderBy(x => Random.Next()));
        }

        #endregion // Helpers

        /// <summary>
        /// Initializes a new game and resets all existing data.
        /// </summary>
        private void NewGame()
        {
            currentState = GameState.Initializing;

            // Reset pile information
            piles = new List<Tuple<int, int>>[2, 6];
            deck = new List<Tuple<int, int>>();
            selectedCardPile = new List<Tuple<int, int>>();
            hoveringCardPile = new List<Tuple<int, int>>();

            // Reset card positions
            movingCardDestinations = new Vector2[2];
            selectedCardPosition = new Vector2();

            // Reset selected card information
            movingCardRects = new Rectangle[2];

            // Reset lerp data
            lerpDelta = -1;
            lerpCompletion = 0;
            pileLerpIndex = 0;
            lerpColumn = 0;
            lerpRow = 0;

            isPileLerped = new bool[2, 6];
            isPileLerpFinished = false;
            isFirstCardLerped = false;

            // Initialize the cards
            InitializeDeck();
            InitializeCardRectangles();
            InitializeCardPiles();
            MediaPlayer.Stop();
        }

        /// <summary>
        /// Initializes the deck of cards used for play and randomly shuffles them.
        /// </summary>
        private void InitializeDeck()
        {
            // Iterate over every card by suit
            for (int i = 0; i < Suits; i++)
            {
                for (int j = 0; j < CardsPerSuit; j++)
                {
                    // Create a new card tuple where Item1 represents
                    // the suit value and Item2 represents the card value
                    Tuple<int, int> card = Tuple.Create(i, j);
                    deck.Add(card);
                }
            }

            // Shuffle the deck after adding in every card
            deck = Shuffle(deck);
        }

        /// <summary>
        /// Partitions the master card texture into individual source rectangles.
        /// </summary>
        private void InitializeCardRectangles()
        {
            // Get the dimensions of each card based on the texture
            cardWidth = cardFaceSheet.Width / CardsPerSuit;
            cardHeight = cardFaceSheet.Height / Suits;

            // Represents the index of an individual card in the master texture
            int cardIndex = 0;

            // Traverse through the dimensions of the master texture to save individual card rectangles
            for (int y = 0; y < cardFaceSheet.Height; y += cardHeight)
            {
                for (int x = 0; x < cardFaceSheet.Width; x += cardWidth)
                {
                    // Get the row (representing suit) of the card in texture
                    int suitNumber = (int)Math.Floor((double)cardIndex / CardsPerSuit);

                    // Get the column (representing card value) of the card in texture
                    int cardValue = cardIndex % CardsPerSuit;

                    // Create a new source rectangle for the card
                    cardRects[suitNumber, cardValue] = new Rectangle(x, y, cardWidth, cardHeight);
                    cardIndex++;
                }
            }
        }

        /// <summary>
        /// Sets up each pile of cards and their positions on the table.
        /// </summary>
        private void InitializeCardPiles()
        {
            // Calculate the starting point of the 2x6 pile layout
            Vector2 cardPosition = new Vector2(0, (GraphicsDevice.Viewport.Bounds.Height / 2) - (cardHeight + (int)cardMargins.Y));

            // Offsets the starting position of the card layout from the left
            Vector2 centreOffSet = new Vector2((float)cardWidth / 2, 0);

            // Set the position of the deck as just left of the card piles
            deckPosition = new Vector2((float)cardWidth / 2, (GraphicsDevice.Viewport.Bounds.Height / 2) - (cardHeight / 2));

            // The entire width of the layout of piles
            float layoutWidth = PileColumns * cardWidth + (PileColumns - 1) * cardMargins.X;

            // Where the first pile to the left starts on the screen
            float layoutStartingOffset = centreOffSet.X + (GraphicsDevice.Viewport.Bounds.Width - layoutWidth) / 2;

            // Iterate over pile
            for (int i = 0; i < PileRows; i++)
            {
                for (int j = 0; j < PileColumns; j++)
                {
                    // Retrieve the top card from the shuffled deck
                    Tuple<int, int> card = Pop(deck);

                    // Initialize the current pile with the retrieved card
                    piles[i, j] = new List<Tuple<int, int>> { card };
                    
                    // Calculates the current card's horizontal position
                    float currentHorizontalOffset = j * cardMargins.X + (j * cardWidth);
                    cardPosition.X = layoutStartingOffset + currentHorizontalOffset;

                    // Set the card position as the final card lerp destination
                    pileDestinations[i, j] = cardPosition;

                    // Initialize where the card starts it's lerp
                    pileStartPositions[i, j] = new Vector2((float)GraphicsDevice.Viewport.Bounds.Width / 2, GraphicsDevice.Viewport.Bounds.Height);
                }

                // Move to next row
                cardPosition.Y += cardHeight + (int)cardMargins.Y;
            }
        }

        /// <summary>
        /// Checks if the player satisfies the win conditions by having every top pile card be a face card.
        /// </summary>
        /// <returns><c>True</c> if player won, otherwise <c>false</c></returns>
        private bool PlayerWon()
        {
            // Iterate over pile
            for (int i = 0; i < PileRows; i++)
            {
                for (int j = 0; j < PileColumns; j++)
                {
                    // Get the top card of each pile
                    Tuple<int, int> card = piles[i, j].LastOrDefault();

                    // Check if the card is less than the minimum requirement value for a face card
                    if (card != null && card.Item2 + 1 < MinFaceCardValue)
                    {
                        return false;
                    }
                }
            }

            // Reset the lerp data if the player won
            lerpDelta = -1;
            lerpCompletion = 0;
            return true;
        }

        /// <summary>
        /// Checks if the player satisfies the losing condition by having no cards that add up 11 (and no face cards that can be turned)
        /// </summary>
        /// <returns><c>True</c> if player lost, otherwise <c>false</c></returns>
        private bool PlayerLost()
        {
            List<int> cardValues = new List<int>();

            // Iterate over all the card piles
            for (int i = 0; i < PileRows; i++)
            {
                for (int j = 0; j < PileColumns; j++)
                {
                    // Get the top pile card
                    Tuple<int, int> card = piles[i, j].LastOrDefault();
                    if (card == null)
                    {
                        continue;
                    }

                    // Check for the case where the player can still turn one of their starting face cards around
                    if (card.Item2 >= MinFaceCardValue && piles[i, j].Count <= 1)
                    {
                        return false;
                    }

                    // Add the card 
                    cardValues.Add(card.Item2 + 1);
                }
            }

            // O(n) approach using a HashSet
            HashSet<int> set = new HashSet<int>();

            // Iterate over all the card values
            foreach (var cardValue in cardValues)
            {
                int temp = RequiredSum - cardValue;

                // Check if the set contains the sum
                if (set.Contains(temp))
                {
                    return false;
                }

                // Add the cardValue to the set
                set.Add(cardValue);
            }

            // Reset the lerp data if the player lost
            lerpDelta = -1;
            lerpCompletion = 0;
            return true;
        }

        /// <summary>
        /// Handles the updates for the main menu screen.
        /// </summary>
        private void UpdateMenuScreen()
        {
            // Store the current and last mouse state to check if the player is clicking
            _lastMouseState = _currentMouseState;
            _currentMouseState = Mouse.GetState();

            // Check if the mouse is not on the button's rectangle
            if (!gameOverButtonRect.Contains(new Point(_currentMouseState.X, _currentMouseState.Y)))
            {
                return;
            }

            // Check if the player is clicking on this frame
            if (_currentMouseState.LeftButton == ButtonState.Pressed && _lastMouseState.LeftButton == ButtonState.Released)
            {
                buttonSound.Play();
                NewGame();
            }
        }

        /// <summary>
        /// Handles updating the game end screen (upon a won or loss).
        /// </summary>
        /// <param name="gameTime"></param>
        private void UpdateGameEndScreen(GameTime gameTime)
        {
            // The Menu Screen has the same button position and functionality as the Game End Screen, so we
            // can just reuse the same code that we used to handle clicking the play button and starting a new game
            UpdateMenuScreen();

            // Check if the lerp hasn't begun
            if (lerpDelta < 0)
            {
                // Check if we are entering the current game state for the first time and play the appropriate sound effect
                if (currentState == GameState.Won)
                {
                    wonSound.Play();
                }
                else if (currentState == GameState.Lost)
                {
                    lostSound.Play();
                }
                
                // Begin the lerp
                lerpDelta = (float)gameTime.TotalGameTime.TotalSeconds;
            }

            // Update our lerp if we haven't finished it
            if (lerpCompletion < maxLerpTime)
            {
                // The speedfactor represents the factor by which we speed our lerp up from 1 second
                float speedFactor = 3;
                lerpCompletion = ((float)gameTime.TotalGameTime.TotalSeconds - lerpDelta) * speedFactor;
            }
        }

        /// <summary>
        /// Handles selecting pairs of cards and manages the bulk of the logic behind the game.
        /// </summary>
        private void UpdateSelecting()
        {
            // Check if win or loss conditions are met
            if (PlayerWon())
            {
                currentState = GameState.Won;
                MediaPlayer.Stop();
                return;
            }

            if (PlayerLost())
            {
                currentState = GameState.Lost;
                MediaPlayer.Stop();
                return;
            }
            
            // Store the current and last mouse state to check if the player is clicking
            _lastMouseState = _currentMouseState;
            _currentMouseState = Mouse.GetState();

            // The pile the user is currently hovering over
            hoveringCardPile = new List<Tuple<int, int>>();
            
            // Iterate over all piles
            for (int i = 0; i < PileRows; i++)
            {
                for (int j = 0; j < PileColumns; j++)
                {
                    // Retrieve the card from the pile and its position
                    Tuple<int, int> card = piles[i, j].LastOrDefault();
                    Vector2 pilePosition = pileDestinations[i, j];

                    // Create a non-texture source rectangle for the card to check if our mouse point is within its boundaries
                    Rectangle cardRectangle = new Rectangle((int)pilePosition.X, (int)pilePosition.Y, cardWidth, cardHeight);

                    if (!cardRectangle.Contains(new Point(_currentMouseState.X, _currentMouseState.Y)))
                    {
                        // Skip if the mouse is not over the current card
                        continue;
                    }

                    // Set hoveringCardPile to the pile that the user's mouse is currently over
                    hoveringCardPile = piles[i, j];

                    if (_currentMouseState.LeftButton != ButtonState.Pressed || _lastMouseState.LeftButton != ButtonState.Released)
                    {
                        // Skip if the current card was not pressed on
                        continue;
                    }

                    // Get the last selected card
                    Tuple<int, int> selectedCard = selectedCardPile.LastOrDefault();
                    
                    if (selectedCard == null)
                    {
                        SelectCard(i, j);
                    }
                    else
                    {
                        PairCard(i, j, selectedCard, card);
                    }

                    // Break from the loop
                    return;
                }
            }
        }

        /// <summary>
        /// Selects a card at a specific position.
        /// </summary>
        /// <param name="cardRow">The row of the card.</param>
        /// <param name="cardColumn">The column of the card.</param>
        private void SelectCard(int cardRow, int cardColumn)
        {
            // Case 1: Update a card
            
            // Retrieve the card's pile and position
            selectedCardPosition = pileDestinations[cardRow, cardColumn];
            selectedCardPile = piles[cardRow, cardColumn];

            // Retrieve the new selected card
            Tuple<int, int> newSelectedCard = selectedCardPile.LastOrDefault();
            if (newSelectedCard == null)
            {
                return;
            }

            // Ignore the card selection if it is not a face card
            if (newSelectedCard.Item2 < MinFaceCardValue)
            {
                return;
            }

            // Case 2: Replace face card in single-element pile

            // Check if the card is the first face card in a pile
            if (selectedCardPile.Count <= 1)
            {
                // Remove the card and add it to the bottom of the deck and then replace it in the pile
                deck.Insert(0, Pop(selectedCardPile));
                selectedCardPile.Insert(0, Pop(deck));

                // Play a random slide sound effect
                slideSounds[Random.Next(slideSounds.Length)].Play();
            }

            // Reset the selected card position if we swapped a bottom face card
            selectedCardPosition = new Vector2();
            selectedCardPile = new List<Tuple<int, int>>();
        }

        /// <summary>
        /// Pairs a newly selected card with a previously selected card.
        /// </summary>
        /// <param name="cardRow">The row of the card.</param>
        /// <param name="cardColumn">The column of the card.</param>
        /// <param name="oldSelectedCard">The previously selected card.</param>
        /// <param name="newSelectedCard">The newly selected card.</param>
        private void PairCard(int cardRow, int cardColumn, Tuple<int, int> oldSelectedCard, Tuple<int, int> newSelectedCard)
        {
            // Case 1: Unselect a card

            // Check if the card pile we selected is the same as the already selected card pile
            if (selectedCardPile == piles[cardRow, cardColumn])
            {
                // Clear the list by creating a new list instance because selectedCardPile is a reference
                selectedCardPosition = new Vector2();
                selectedCardPile = new List<Tuple<int, int>>();
            }

            // Check if both cards don't have a value that adds up to 11 (subtract 2 because each value is zero-based)
            if (oldSelectedCard.Item2 + newSelectedCard.Item2 != RequiredSum - 2)
            {
                return;
            }

            // Take the next two cards from the deck and place then at the top of each pile
            selectedCardPile.Add(Pop(deck));
            piles[cardRow, cardColumn].Add(Pop(deck));
            
            // Set the moving card destinations for lerping the two newly added cards
            movingCardDestinations[0] = pileDestinations[cardRow, cardColumn];
            movingCardDestinations[1] = selectedCardPosition;
            
            // Get the new top cards and their rectangles
            Tuple<int, int> newCard1 = piles[cardRow, cardColumn].LastOrDefault();
            if (newCard1 != null)
            {
                movingCardRects[0] = cardRects[newCard1.Item1, newCard1.Item2];
            }

            Tuple<int, int> newCard2 = selectedCardPile.LastOrDefault();
            if (newCard2 != null)
            {
                movingCardRects[1] = cardRects[newCard2.Item1, newCard2.Item2];
            }

            // Reset lerp data
            lerpDelta = -1;
            lerpCompletion = 0;

            // Reset selected card data
            selectedCardPosition = new Vector2();
            selectedCardPile = new List<Tuple<int, int>>();

            currentState = GameState.Moving;
        }

        /// <summary>
        /// Updates the game during initialization and handles lerping the piles.
        /// </summary>
        /// <param name="gameTime">The time of the game.</param>
        private void UpdateGameInitialization(GameTime gameTime)
        {
            // Get the row and column of the card based on the card lerp index
            lerpColumn = (int)Math.Floor((double)pileLerpIndex / PileColumns);
            lerpRow = pileLerpIndex % PileColumns;

            // Check if the lerp hasn't begun
            if (lerpDelta < 0)
            {
                lerpDelta = (float)gameTime.TotalGameTime.TotalSeconds;
            }
            
            // The speedfactor represents the factor by which we speed our lerp up from 1 second
            float speedFactor = 12;
            lerpCompletion = ((float)gameTime.TotalGameTime.TotalSeconds - lerpDelta) * speedFactor;

            // Check if the pile lerp is complete and all the piles are at their destinations
            if (isPileLerpFinished)
            {
                // Reset lerp data and move on to the next gameplay state
                lerpDelta = -1;
                lerpCompletion = 0;

                currentState = GameState.Selecting;
            }

            // Check if the lerp for the current pile is complete and make sure all the piles are not done
            if (lerpCompletion >= maxLerpTime && !isPileLerpFinished)
            {
                // Reset the lerp delta and mark the current pile as completed
                lerpDelta = -1;
                isPileLerped[lerpColumn, lerpRow] = true;

                // Check if every pile has been lerped
                if (pileLerpIndex == (isPileLerped.GetLength(0) * isPileLerped.GetLength(1)) - 1)
                {
                    isPileLerpFinished = true;
                }

                // Move onto the next pile
                pileLerpIndex++;
            }
        }

        /// <summary>
        /// Handles the lerping of new cards after being paired.
        /// </summary>
        /// <param name="gameTime">The time of the game.</param>
        private void UpdateMoving(GameTime gameTime)
        {
            // Check if the lerp hasn't begun
            if (lerpDelta < 0)
            {
                lerpDelta = (float)gameTime.TotalGameTime.TotalSeconds;
            }

            // The speedfactor represents the factor by which we speed our lerp up from 1 second
            float speedFactor = 2;
            lerpCompletion = ((float)gameTime.TotalGameTime.TotalSeconds - lerpDelta) * speedFactor;

            // Check if the lerp is not completed
            if (lerpCompletion < maxLerpTime)
            {
                return;
            }

            // Play a random slide sound effect
            slideSounds[Random.Next(slideSounds.Length)].Play();

            // Reset the lerp delta
            lerpDelta = -1;
            lerpCompletion = 0;

            if (isFirstCardLerped)
            {
                // The second card has finished lerping and we can move onto the next state

                isFirstCardLerped = false;
                currentState = GameState.Selecting;
                return;
            }

            // The first card has finished lerping
            isFirstCardLerped = true;
        }

        /// <summary>
        /// Draws the game end screens for both the lost and won conditions.
        /// </summary>
        private void DrawGameEndScreen()
        {
            // Draw the background texture, the deck, and every current pile
            spriteBatch.Draw(backgroundTexture, backgroundRect, Color.White);

            for (int i = 0; i < PileRows; i++)
            {
                for (int j = 0; j < PileColumns; j++)
                {
                    Tuple<int, int> card = piles[i, j].LastOrDefault();
                    if (card != null)
                    {
                        spriteBatch.Draw(cardFaceSheet, pileDestinations[i, j], cardRects[card.Item1, card.Item2], Color.White);
                    }
                }
            }

            spriteBatch.Draw(cardBackTexture, deckPosition, Color.White);
            spriteBatch.DrawString(defaultFont, $"Deck: {deck.Count}", deckPosition - deckTextOffset, Color.White);
        }

        /// <summary>
        /// Draws the main menu.
        /// </summary>
        private void DrawMenuScreen()
        {
            spriteBatch.Draw(menuScreenTexture, backgroundRect, Color.White);
        }

        /// <summary>
        /// Draws the losing screen.
        /// </summary>
        private void DrawLostScreen()
        {
            // Draw the base game end screen and the loss-specific menu texture
            DrawGameEndScreen();
            spriteBatch.Draw(lostScreenTexture, backgroundRect, new Color(Color.White, lerpCompletion));
        }

        /// <summary>
        /// Draws the winning screen.
        /// </summary>
        private void DrawWonScreen()
        {
            // Draw the base game end screen and the win-specific menu texture
            DrawGameEndScreen();
            spriteBatch.Draw(winScreenTexture, backgroundRect, new Color(Color.White, lerpCompletion));
        }

        /// <summary>
        /// Handles drawing cards lerping towards their appropriate pile.
        /// </summary>
        private void DrawMoving()
        {
            spriteBatch.Draw(backgroundTexture, backgroundRect, Color.White);

            // Iterate over each piles
            for (int i = 0; i < PileRows; i++)
            {
                for (int j = 0; j < PileColumns; j++)
                {
                    // Get the top card of each pile
                    Tuple<int, int> card = piles[i, j].LastOrDefault();
                    if (card == null)
                    {
                        continue;
                    }

                    // Get the card rectangle
                    Rectangle cardRect = cardRects[card.Item1, card.Item2];

                    // Check if the current pile is one of the moving non-lerped rectangles
                    if (cardRect == movingCardRects[1] || cardRect == movingCardRects[0] && !isFirstCardLerped)
                    {
                        // Check if the pile has more than one element
                        if (piles[i, j].Count >= 2)
                        {
                            // Get the second last card in the pile and set the cardRect to it
                            card = piles[i, j].Last(x => Equals(x, piles[i, j][piles[i, j].Count - 2]));
                            cardRect = cardRects[card.Item1, card.Item2];
                        }
                    }
                    
                    // Draw the card
                    spriteBatch.Draw(cardFaceSheet, pileDestinations[i, j], cardRect, Color.White);
                }
            }

            // Check if the first cart was already lerped
            int lerpIndex = isFirstCardLerped ? 1 : 0;

            // Draw the lerping card and deck
            spriteBatch.Draw(cardFaceSheet, Vector2.Lerp(deckPosition, movingCardDestinations[lerpIndex], lerpCompletion > 1 ? 1 : lerpCompletion), movingCardRects[lerpIndex], Color.White);
            spriteBatch.Draw(cardBackTexture, deckPosition, Color.White);
            spriteBatch.DrawString(defaultFont, $"Deck: {deck.Count}", deckPosition - deckTextOffset, Color.White);
        }

        /// <summary>
        /// Draws the table layout while selecting a card.
        /// </summary>
        private void DrawSelecting()
        {
            spriteBatch.Draw(backgroundTexture, backgroundRect, Color.White);

            // Iterate over all the piles
            for (int i = 0; i < PileRows; i++)
            {
                for (int j = 0; j < PileColumns; j++)
                {
                    // Get the card from the top of the pile
                    Tuple<int, int> card = piles[i, j].LastOrDefault();
                    if (card == null)
                    {
                        continue;
                    }

                    // If the user is selecting the current card pile
                    if (selectedCardPile == piles[i, j])
                    {
                        spriteBatch.Draw(cardFaceSheet, pileDestinations[i, j], cardRects[card.Item1, card.Item2], Color.Gray);
                    }
                    // If user is hovering over the current card pile
                    else if (hoveringCardPile == piles[i,j])
                    {
                        spriteBatch.Draw(cardFaceSheet, pileDestinations[i, j], cardRects[card.Item1, card.Item2], Color.LightGray);
                    }
                    // The default case where the user is not hovering over or selecting the current card pile
                    else
                    {
                        spriteBatch.Draw(cardFaceSheet, pileDestinations[i, j], cardRects[card.Item1, card.Item2], Color.White);
                    }
                }
            }

            // Draw the deck
            spriteBatch.Draw(cardBackTexture, deckPosition, Color.White);
            spriteBatch.DrawString(defaultFont, $"Deck: {deck.Count}", deckPosition - deckTextOffset, Color.White);
        }

        /// <summary>
        /// Draws the game while it is initializing the cards and lerping the piles.
        /// </summary>
        private void DrawGameInitialization()
        {
            spriteBatch.Draw(backgroundTexture, backgroundRect, Color.White);

            // Draw the already lerped piles
            DrawLerpedPiles();
            Tuple<int, int> card = piles[lerpColumn, lerpRow].LastOrDefault();
            
            // Draw the currently lerping pile.
            // Check to make sure the lerpCompletion value is not above 1, and set it to 1 if it is
            if (card != null)
            { 
                spriteBatch.Draw(cardFaceSheet, Vector2.Lerp(pileStartPositions[lerpColumn, lerpRow],
                           pileDestinations[lerpColumn, lerpRow], lerpCompletion > 1 ? 1 : lerpCompletion), cardRects[card.Item1, card.Item2], Color.White);
            }

            spriteBatch.Draw(cardBackTexture, deckPosition, Color.White);
            spriteBatch.DrawString(defaultFont, $"Deck: {deck.Count}", deckPosition - deckTextOffset, Color.White);
        }

        /// <summary>
        /// Draws the fully lerped cards.
        /// </summary>
        private void DrawLerpedPiles()
        {
            // Iterate over all the piles
            for (int i = 0; i < PileRows; i++)
            {
                for (int j = 0; j < PileColumns; j++)
                {
                    if (isPileLerped[i, j] == false)
                    {
                        // Break from the nested loop when the current card is still lerping or unlerped
                        return;
                    }

                    // Draw the already lerped card
                    Tuple<int, int> lerpedCard = piles[i, j].LastOrDefault();
                    if (lerpedCard == null)
                    {
                        continue;
                    }

                    spriteBatch.Draw(cardFaceSheet, pileDestinations[i, j], cardRects[lerpedCard.Item1, lerpedCard.Item2], Color.White);
                }
            }
        }
    }

    #endregion // Methods
}
