using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Microsoft.Xna.Framework.Media;
using BugSense;

namespace BugSense.XnaTestApp {
    /// <summary>
    /// This is the main type for your game
    /// </summary>

#if WINDOWS_RT
    public class Game1 : IDisposable
    {
        public static Game1 Instance;
        private GameTimer _timer;
        private SharedGraphicsDeviceManager _graphicsManager;
        public GraphicsDevice GraphicsDevice { get; private set; }
        public GameServiceContainer Services { get; private set; }
        public ContentManager Content;
#else
    public class Game1 : Microsoft.Xna.Framework.Game
    {
        GraphicsDeviceManager _graphicsManager;
#endif

        SpriteBatch spriteBatch;

#if WINDOWS_RT
        public Game1()
        {
            Services = new GameServiceContainer();

            _graphicsManager = new SharedGraphicsDeviceManager();
            Services.AddService(typeof(IGraphicsDeviceService), _graphicsManager);
            Content = new ContentManager(Services, "Content");
            Instance = this;
        }
#else
        public Game1()
        {
            _graphicsManager = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";

            // Frame rate is 30 fps by default for Windows Phone.
            TargetElapsedTime = TimeSpan.FromTicks(333333);

            // Extend battery life under lock.
            InactiveSleepTime = TimeSpan.FromSeconds(1);
        }

        void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            throw new NotImplementedException();
        }
#endif


        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
#if WINDOWS_RT
        public void Initialize()
#else
        protected override void Initialize()
#endif
        {
            // TODO: Add your initialization logic here

#if WINDOWS_RT

            GraphicsDevice = _graphicsManager.GraphicsDevice;
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);
            BugSenseHandler.Instance.Init("e1821f8f");
            BugSenseHandler.Instance.ScreenOrientation = "Landscape";
            var pp = GraphicsDevice.PresentationParameters;
            BugSenseHandler.Instance.ScreenSize.X = pp.BackBufferWidth;
            BugSenseHandler.Instance.ScreenSize.Y = pp.BackBufferHeight;
            

            BugSenseHandler.LogError(new Exception("WINRT Exception"), "This is an error");
            
            _timer = new GameTimer();
            _timer.UpdateInterval = TimeSpan.Zero;
            _timer.Draw += (o, a) => Draw(new GameTime(a.TotalTime, a.ElapsedTime));
            //_timer.Update += (o, a) => Update(new GameTime(a.TotalTime, a.ElapsedTime));
            _timer.Start();
#else
            base.Initialize();
#endif

            
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
#if WINDOWS_RT
#else
        protected override void LoadContent()

        
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);

            BugSense.BugSenseHandler.Instance.Init("71d1f500");
            BugSenseHandler.LogError(new Exception(), "This is an error");
            // TODO: use this.Content to load your game content here
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {

            // Allows the game to exit
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
                this.Exit();

            // TODO: Add your update logic here

            base.Update(gameTime);
        }
#endif


        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
#if WINDOWS_RT
        protected void Draw(GameTime gameTime)
#else
        protected override void Draw(GameTime gameTime)
#endif
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

#if WINDOWS_RT
#else
            // TODO: Add your drawing code here
            base.Draw(gameTime);
#endif
        }

#if WINDOWS_RT
        public void Dispose()
        {
            Content.Unload();
        }
#endif
    }
}
