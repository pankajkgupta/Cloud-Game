//#define SREENSHOT_PAUSE


using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

using Microsoft.Kinect;
using System.IO;

using Microsoft.Speech.AudioFormat;
using Microsoft.Speech.Recognition;

namespace CloudGame
{
    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class CloudGame : Microsoft.Xna.Framework.Game
    {

        // Game World

#if SREENSHOT_PAUSE

        // Used to pause the screen after a number of skeleton tracked events
        // This is so that I can take screenshots 
        // The trackcount and limit
        int trackCount = 0;
        int trackLimit = 200;

#endif

        #region Speech recognition
        RecognizerInfo kinectRecognizerInfo;
        SpeechRecognitionEngine recognizer;

        KinectAudioSource kinectSource;

        Stream audioStream;

        private RecognizerInfo findKinectRecognizerInfo()
        {
            //var recognizers = SpeechRecognitionEngine.InstalledRecognizers();

            foreach (RecognizerInfo recInfo in SpeechRecognitionEngine.InstalledRecognizers())
            {
                // look at each recognizer info value to find the one that works for Kinect
                if (recInfo.AdditionalInfo.ContainsKey("Kinect"))
                {
                    string details = recInfo.AdditionalInfo["Kinect"];
                    if (details == "True" && recInfo.Culture.Name == "en-US")
                    {
                        // If we get here we have found the info we want to use
                        return recInfo;
                    }
                }
            }
            return null;
        }

        private bool createSpeechEngine()
        {
            kinectRecognizerInfo = findKinectRecognizerInfo();

            if (kinectRecognizerInfo == null)
            {
                errorMessage = "Kinect recognizer not found";
                return false;
            }

            try
            {
                recognizer = new SpeechRecognitionEngine(kinectRecognizerInfo);
            }
            catch
            {
                errorMessage = "Speech recognition engine could not be loaded";
                return false;
            }

            return true;
        }

        private void buildCommands()
        {
            Choices commands = new Choices();

            commands.Add("Red");
            commands.Add("Green");
            commands.Add("Blue");
            commands.Add("Yellow");
            commands.Add("Cyan");
            commands.Add("Orange");
            commands.Add("Purple");

            GrammarBuilder grammarBuilder = new GrammarBuilder();

            grammarBuilder.Culture = kinectRecognizerInfo.Culture;
            grammarBuilder.Append(commands);

            Grammar grammar = new Grammar(grammarBuilder);

            recognizer.LoadGrammar(grammar);
        }

        private bool setupAudio()
        {
            try
            {
                kinectSource = myKinect.AudioSource;
                kinectSource.BeamAngleMode = BeamAngleMode.Adaptive;
                audioStream = kinectSource.Start();
                recognizer.SetInputToAudioStream(audioStream, new SpeechAudioFormatInfo(
                                                      EncodingFormat.Pcm, 16000, 16, 1,
                                                      32000, 2, null));
                recognizer.RecognizeAsync(RecognizeMode.Multiple);
            }
            catch
            {
                errorMessage = "Audio stream could not be connected";
                return false;
            }
            return true;
        }

        private bool SetupSpeechRecognition()
        {
            if (!createSpeechEngine()) return false;

            buildCommands();

            if (!setupAudio()) return false;

            recognizer.SpeechRecognized +=
                new EventHandler<SpeechRecognizedEventArgs>(recognizer_SpeechRecognized);

            return true;
        }

        void recognizer_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            if (e.Result.Confidence < 0.95f) return;

            switch (e.Result.Text)
            {
                case "Red":
                    pinColor = Color.Red;
                    break;
                case "Green":
                    pinColor = Color.Green;
                    break;
                case "Blue":
                    pinColor = Color.Blue;
                    break;
                case "Yellow":
                    pinColor = Color.Yellow;
                    break;
                case "Cyan":
                    pinColor = Color.Cyan;
                    break;
                case "Orange":
                    pinColor = Color.Orange;
                    break;
                case "Purple":
                    pinColor = Color.Purple;
                    break;
            }
        }

        #endregion

        #region Clouds and Sprites

        interface ISprite
        {
            void Draw(CloudGame game);
            void Update(CloudGame game);
        }

        class Cloud : CloudGame.ISprite
        {
            public Texture2D CloudTexture;
            public Vector2 CloudPosition;
            public Vector2 CloudSpeed;
            public bool Burst = false;
            public SoundEffect CloudPopSound;

            static Random rand = new Random();

            public void Draw(CloudGame game)
            {
                if (!Burst)
                    game.spriteBatch.Draw(CloudTexture, CloudPosition, Color.White);
            }

            public void Update(CloudGame game)
            {
                if (Burst) return;

                CloudPosition += CloudSpeed;

                if (CloudPosition.X > game.GraphicsDevice.Viewport.Width)
                {
                    CloudPosition.X = -CloudTexture.Width;
                    CloudPosition.Y = rand.Next(game.GraphicsDevice.Viewport.Height - CloudTexture.Height);
                }

                if (CloudContains(game.PinVector))
                {
                    CloudPopSound.Play();
                    Burst = true;
                    return;
                }
            }

            public bool CloudContains(Vector2 pos)
            {
                if (pos.X < CloudPosition.X) return false;
                if (pos.X > (CloudPosition.X + CloudTexture.Width)) return false;
                if (pos.Y < CloudPosition.Y) return false;
                if (pos.Y > (CloudPosition.Y + CloudTexture.Height)) return false;
                return true;
            }

            public Cloud(Texture2D inTexture, Vector2 inPosition, Vector2 inSpeed, SoundEffect inPop)
            {
                CloudTexture = inTexture;
                CloudPosition = inPosition;
                CloudSpeed = inSpeed;
                CloudPopSound = inPop;
            }
        }


        List<ISprite> gameSprites = new List<ISprite>();

        #endregion

        #region Kinect

        KinectSensor myKinect;

        SpriteFont messageFont;

        string errorMessage = "";

        protected bool setupKinect()
        {
            // Check to see if a Kinect is available
            if (KinectSensor.KinectSensors.Count == 0)
            {
                errorMessage = "No Kinects detected";
                return false;
            }

            // Get the first Kinect on the computer
            myKinect = KinectSensor.KinectSensors[0];

            // Start the Kinect running and select all the streams
            try
            {
                myKinect.SkeletonStream.Enable();
                myKinect.ColorStream.Enable();
                myKinect.DepthStream.Enable(DepthImageFormat.Resolution320x240Fps30);
                myKinect.Start();
            }
            catch
            {
                errorMessage = "Kinect initialise failed";
                return false;
            }

            // connect a handler to the event that fires when new frames are available

            myKinect.AllFramesReady += new EventHandler<AllFramesReadyEventArgs>(myKinect_AllFramesReady);

            return true;
        }

        #endregion

        #region Image Processing

        byte[] colorData = null;
        short[] depthData = null;

        Texture2D gameMaskTexture = null;
        Texture2D kinectVideoTexture;
        Rectangle fullScreenRectangle;

        Texture2D gameImageTexture;
        Color[] maskImageColors = null;

        Skeleton[] skeletons = null;
        Skeleton activeSkeleton = null;

        int activeSkeletonNumber;

        void myKinect_AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
#if SREENSHOT_PAUSE
            if (trackCount == trackLimit) return;
#endif

            #region Video image

            // Puts a copy of the video image into the kinect video texture

            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (colorFrame == null)
                    return;

                if (colorData == null)
                    colorData = new byte[colorFrame.Width * colorFrame.Height * 4];

                colorFrame.CopyPixelDataTo(colorData);

                kinectVideoTexture = new Texture2D(GraphicsDevice, colorFrame.Width, colorFrame.Height);

                Color[] bitmap = new Color[colorFrame.Width * colorFrame.Height];

                int sourceOffset = 0;

                for (int i = 0; i < bitmap.Length; i++)
                {
                    bitmap[i] = new Color(colorData[sourceOffset + 2],
                        colorData[sourceOffset + 1], colorData[sourceOffset], 255);
                    sourceOffset += 4;
                }

                kinectVideoTexture.SetData(bitmap);
            }

            #endregion

            #region Skeleton

            // Finds the currently active skeleton

            using (SkeletonFrame frame = e.OpenSkeletonFrame())
            {
                if (frame == null)
                    return;
                else
                {
                    skeletons = new Skeleton[frame.SkeletonArrayLength];
                    frame.CopySkeletonDataTo(skeletons);
                }
            }

            activeSkeletonNumber = 0;

            for (int i = 0; i < skeletons.Length; i++)
            {
                if (skeletons[i].TrackingState == SkeletonTrackingState.Tracked)
                {
                    activeSkeletonNumber = i + 1;
                    activeSkeleton = skeletons[i];
                    break;
                }
            }

            #endregion

            #region Depth image

            // Creates a game background image with transparent regions 
            // where the player is displayed

            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                // Get the depth data

                if (depthFrame == null) return;

                if (depthData == null)
                    depthData = new short[depthFrame.Width * depthFrame.Height];

                depthFrame.CopyPixelDataTo(depthData);

                // Create the mask from the background image

                gameImageTexture.GetData(maskImageColors);

                if (activeSkeletonNumber != 0)
                {
                    for (int depthPos = 0; depthPos < depthData.Length; depthPos++)
                    {
                        // find a player to mask - split off bottom bits
                        int playerNo = depthData[depthPos] & 0x07;

                        if (playerNo == activeSkeletonNumber)
                        {
                            // We have a player to mask

                            // find the X and Y positions of the depth point
                            int x = depthPos % depthFrame.Width;
                            int y = depthPos / depthFrame.Width;

                            // get the X and Y positions in the video feed
                            ColorImagePoint playerPoint = myKinect.MapDepthToColorImagePoint(
                                DepthImageFormat.Resolution320x240Fps30, x, y, depthData[depthPos], ColorImageFormat.RgbResolution640x480Fps30);

                            // Map the player coordinates into our lower resolution background
                            // Have to do this because the lowest resultion for the color camera is 640x480

                            playerPoint.X /= 2;
                            playerPoint.Y /= 2;

                            // convert this into an offset into the mask color data
                            int gameImagePos = (playerPoint.X + (playerPoint.Y * depthFrame.Width));
                            if (gameImagePos < maskImageColors.Length)
                                // make this point in the mask transparent
                                maskImageColors[gameImagePos] = Color.FromNonPremultiplied(0, 0, 0, 0);
                        }
                    }
                }

                gameMaskTexture = new Texture2D(GraphicsDevice, depthFrame.Width, depthFrame.Height);
                gameMaskTexture.SetData(maskImageColors);

            }

            #endregion

        }

        Color boneColor = Color.White;

        Texture2D lineDot;

        void drawLine(Vector2 v1, Vector2 v2, Color col)
        {
            Vector2 origin = new Vector2(0.5f, 0.0f);
            Vector2 diff = v2 - v1;
            float angle;
            Vector2 scale = new Vector2(1.0f, diff.Length() / lineDot.Height);
            angle = (float)(Math.Atan2(diff.Y, diff.X)) - MathHelper.PiOver2;
            spriteBatch.Draw(lineDot, v1, null, col, angle, origin, scale, SpriteEffects.None, 1.0f);
        }

        void drawBone(Joint j1, Joint j2, Color col)
        {
            ColorImagePoint j1P = myKinect.MapSkeletonPointToColor(
                j1.Position,
                ColorImageFormat.RgbResolution640x480Fps30);
            Vector2 j1V = new Vector2(j1P.X, j1P.Y);

            ColorImagePoint j2P = myKinect.MapSkeletonPointToColor(
                j2.Position,
                ColorImageFormat.RgbResolution640x480Fps30);
            Vector2 j2V = new Vector2(j2P.X, j2P.Y);

            drawLine(j1V, j2V, col);
        }

        void drawSkeleton(Skeleton skel, Color col)
        {
            // Spine
            drawBone(skel.Joints[JointType.Head], skel.Joints[JointType.ShoulderCenter], col);
            drawBone(skel.Joints[JointType.ShoulderCenter], skel.Joints[JointType.Spine], col);

            // Left leg
            drawBone(skel.Joints[JointType.Spine], skel.Joints[JointType.HipCenter], col);
            drawBone(skel.Joints[JointType.HipCenter], skel.Joints[JointType.HipLeft], col);
            drawBone(skel.Joints[JointType.HipLeft], skel.Joints[JointType.KneeLeft], col);
            drawBone(skel.Joints[JointType.KneeLeft], skel.Joints[JointType.AnkleLeft], col);
            drawBone(skel.Joints[JointType.AnkleLeft], skel.Joints[JointType.FootLeft], col);

            // Right leg
            drawBone(skel.Joints[JointType.HipCenter], skel.Joints[JointType.HipRight], col);
            drawBone(skel.Joints[JointType.HipRight], skel.Joints[JointType.KneeRight], col);
            drawBone(skel.Joints[JointType.KneeRight], skel.Joints[JointType.AnkleRight], col);
            drawBone(skel.Joints[JointType.AnkleRight], skel.Joints[JointType.FootRight], col);

            // Left arm
            drawBone(skel.Joints[JointType.ShoulderCenter], skel.Joints[JointType.ShoulderLeft], col);
            drawBone(skel.Joints[JointType.ShoulderLeft], skel.Joints[JointType.ElbowLeft], col);
            drawBone(skel.Joints[JointType.ElbowLeft], skel.Joints[JointType.WristLeft], col);
            drawBone(skel.Joints[JointType.WristLeft], skel.Joints[JointType.HandLeft], col);

            // Right arm
            drawBone(skel.Joints[JointType.ShoulderCenter], skel.Joints[JointType.ShoulderRight], col);
            drawBone(skel.Joints[JointType.ShoulderRight], skel.Joints[JointType.ElbowRight], col);
            drawBone(skel.Joints[JointType.ElbowRight], skel.Joints[JointType.WristRight], col);
            drawBone(skel.Joints[JointType.WristRight], skel.Joints[JointType.HandRight], col);
        }

        #endregion

        #region Pin management

        Texture2D pinTexture;
        Rectangle pinRectangle;
        Color pinColor = Color.Red;

        public int PinX, PinY;
        public Vector2 PinVector;

        JointType pinJoint = JointType.HandRight;

        void updatePin()
        {
            if (activeSkeletonNumber == 0)
            {
                PinX = -100;
                PinY = -100;
            }
            else
            {
                Joint joint = activeSkeleton.Joints[pinJoint];

                ColorImagePoint pinPoint = myKinect.MapSkeletonPointToColor(
                    joint.Position,
                    ColorImageFormat.RgbResolution640x480Fps30);

                PinX = pinPoint.X;
                PinY = pinPoint.Y;
            }

            PinVector.X = PinX;
            PinVector.Y = PinY;

            pinRectangle.X = PinX - pinRectangle.Width / 2;
            pinRectangle.Y = PinY - pinRectangle.Height / 2;
        }

        #endregion

        Random rand = new Random();

        int noOfSprites = 50;

        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;

        public CloudGame()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";

            // Make the screen the same size as the video display output
            graphics.PreferredBackBufferHeight = 480;
            graphics.PreferredBackBufferWidth = 640;
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            pinRectangle = new Rectangle(0, 0, GraphicsDevice.Viewport.Width / 20, GraphicsDevice.Viewport.Width / 20);

            fullScreenRectangle = new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);

            base.Initialize();
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);

            Texture2D cloudTexture = Content.Load<Texture2D>("Cloud");
            SoundEffect cloudPop = Content.Load<SoundEffect>("Pop");
            pinTexture = Content.Load<Texture2D>("pin");
            messageFont = Content.Load<SpriteFont>("MessageFont");
            lineDot = Content.Load<Texture2D>("whiteDot");

            gameImageTexture = Content.Load<Texture2D>("CloudGameBackground");
            maskImageColors = new Color[gameImageTexture.Width * gameImageTexture.Height];

            setupKinect();

            SetupSpeechRecognition();


            for (int i = 0; i < noOfSprites; i++)
            {
                Vector2 position =
                    new Vector2(rand.Next(GraphicsDevice.Viewport.Width),
                                rand.Next(GraphicsDevice.Viewport.Height));

                // Parallax scrolling of clouds
                Vector2 speed = new Vector2(i / 6f, 0);

                Cloud c = new Cloud(cloudTexture, position, speed, cloudPop);

                gameSprites.Add(c);
            }

        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
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
#if SREENSHOT_PAUSE
            if (trackCount == trackLimit) return;
#endif

            // Allows the game to exit
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
                this.Exit();

            updatePin();

            foreach (ISprite sprite in gameSprites)
                sprite.Update(this);

            base.Update(gameTime);
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            spriteBatch.Begin();

            if (kinectVideoTexture != null)
                spriteBatch.Draw(kinectVideoTexture, fullScreenRectangle, Color.White);

            if (gameMaskTexture != null)
                spriteBatch.Draw(gameMaskTexture, fullScreenRectangle, Color.White);

            foreach (ISprite sprite in gameSprites)
                sprite.Draw(this);

            if (activeSkeleton != null)
            {
                drawSkeleton(activeSkeleton, Color.White);
            }

            spriteBatch.Draw(pinTexture, pinRectangle, pinColor);

            if (errorMessage.Length > 0)
            {
                spriteBatch.DrawString(messageFont, errorMessage, Vector2.Zero, Color.White);
            }

            spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
