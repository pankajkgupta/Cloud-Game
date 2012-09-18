//#define SREENSHOT_PAUSE

#region Using Statements
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

using BeTheController;
#endregion

namespace CloudGame
{
    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class CloudGame : Microsoft.Xna.Framework.Game
    {

        #region Fields

        /// <summary>
        /// Stores the view matrix for the model, which gets the model
        /// in the right place, relative to the camera.
        /// </summary>
        Matrix view = Matrix.CreateLookAt(new Vector3(0, 0, -20), new Vector3(0, 0, 100), Vector3.Up);
        Matrix projection;
        Random rand = new Random();
        int animationDepth = -1;
        int animationHeight = 1;
        int animationWidth = 1;
        int noOfSprites = 50;

        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;
        BTCRunTime BTC = new BTCRunTime();
        bool swortRight = false;
        bool swortLeft = false;
        bool startcastingRight = false;
        bool startcastingLeft = false;
        int ii = 0;
        Color start = Color.Red;
        Color nect = Color.White;
        // Store a list of primitive models, plus which one is currently selected.
        List<GeometricPrimitive> primitives = new List<GeometricPrimitive>();
        int currentPrimitiveIndex = 0;
        

#if SREENSHOT_PAUSE

        // Used to pause the screen after a number of skeleton tracked events
        // This is so that I can take screenshots 
        // The trackcount and limit
        int trackCount = 0;
        int trackLimit = 200;

#endif
        #endregion

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
            private Model model;

            public Texture2D CloudTexture;
            public Vector3 CloudPosition;
            public Vector3 CloudSpeed;
            public bool Burst = false;
            public SoundEffect CloudPopSound;

            static Random rand = new Random();

            public void Draw(CloudGame game)
            {
                if (!Burst)
                {
                    //game.spriteBatch.Draw(CloudTexture, CloudPosition, Color.White);
                    Matrix world = Matrix.CreateTranslation(CloudPosition);
                    game.DrawModel(model, world, game.view, game.projection);
                }
            }



            public void Update(CloudGame game)
            {
                if (Burst) return;

                CloudPosition += CloudSpeed;

                if (CloudPosition.X > game.GraphicsDevice.Viewport.Width/10)
                {
                    CloudPosition.X = -15;
                    CloudPosition.Y = rand.Next(game.animationHeight);
                    CloudPosition.Z = rand.Next(game.animationDepth);
                }

                //if (CloudContains(game.PinVector))
                //{
                //    CloudPopSound.Play();
                //    Burst = true;
                //    return;
                //}
                Matrix cloudWorldMatrix = Matrix.CreateTranslation(CloudPosition);

                if (IsCollision(model, cloudWorldMatrix, game.pinModel, game.pinModelMatrix))
                {
                    CloudPopSound.Play();
                    Burst = true;
                    return;
                }
            }

            public bool CloudContains(Vector3 pos)
            {
                if (pos.X < CloudPosition.X) return false;
                if (pos.X > (CloudPosition.X + CloudTexture.Width)) return false;
                if (pos.Y < CloudPosition.Y) return false;
                if (pos.Y > (CloudPosition.Y + CloudTexture.Height)) return false;
                return true;
            }

            private bool IsCollision(Model model1, Matrix world1, Model model2, Matrix world2)
            {
                for (int meshIndex1 = 0; meshIndex1 < model1.Meshes.Count; meshIndex1++)
                {
                    BoundingSphere sphere1 = model1.Meshes[meshIndex1].BoundingSphere;
                    sphere1 = sphere1.Transform(world1);

                    for (int meshIndex2 = 0; meshIndex2 < model2.Meshes.Count; meshIndex2++)
                    {
                        BoundingSphere sphere2 = model2.Meshes[meshIndex2].BoundingSphere;
                        sphere2 = sphere2.Transform(world2);

                        if (sphere1.Intersects(sphere2))
                            return true;
                    }
                }
                return false;
            }

            public Cloud(Texture2D inTexture, Model inModel, Vector3 inPosition, Vector3 inSpeed, SoundEffect inPop)
            {
                CloudTexture = inTexture;
                model = inModel;
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

            myKinect = BTC.GetKinectNui();
            //We have Kinect Connected
            if (myKinect != null)
            {
                BTC.OnEventRecognized += new BTCRunTime.EventRecognizedEventHandler(Game_OnEventRecognized);
                BTC.OnEventStopRecognized += new BTCRunTime.EventStopRecognizedEventHandler(Game_OnEventStopRecognized);
                myKinect.AllFramesReady += new EventHandler<AllFramesReadyEventArgs>(myKinect_AllFramesReady);
            }

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

            //// Creates a game background image with transparent regions 
            //// where the player is displayed

            //using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            //{
            //    // Get the depth data

            //    if (depthFrame == null) return;

            //    if (depthData == null)
            //        depthData = new short[depthFrame.Width * depthFrame.Height];

            //    depthFrame.CopyPixelDataTo(depthData);

            //    // Create the mask from the background image

            //    gameImageTexture.GetData(maskImageColors);

            //    if (activeSkeletonNumber != 0)
            //    {
            //        for (int depthPos = 0; depthPos < depthData.Length; depthPos++)
            //        {
            //            // find a player to mask - split off bottom bits
            //            int playerNo = depthData[depthPos] & 0x07;

            //            if (playerNo == activeSkeletonNumber)
            //            {
            //                // We have a player to mask

            //                // find the X and Y positions of the depth point
            //                int x = depthPos % depthFrame.Width;
            //                int y = depthPos / depthFrame.Width;

            //                // get the X and Y positions in the video feed
            //                ColorImagePoint playerPoint = myKinect.MapDepthToColorImagePoint(
            //                    DepthImageFormat.Resolution320x240Fps30, x, y, depthData[depthPos], ColorImageFormat.RgbResolution640x480Fps30);

            //                // Map the player coordinates into our lower resolution background
            //                // Have to do this because the lowest resultion for the color camera is 640x480

            //                playerPoint.X /= 2;
            //                playerPoint.Y /= 2;

            //                // convert this into an offset into the mask color data
            //                int gameImagePos = (playerPoint.X + (playerPoint.Y * depthFrame.Width));
            //                if (gameImagePos < maskImageColors.Length)
            //                    // make this point in the mask transparent
            //                    maskImageColors[gameImagePos] = Color.FromNonPremultiplied(0, 0, 0, 0);
            //            }
            //        }
            //    }

            //    gameMaskTexture = new Texture2D(GraphicsDevice, depthFrame.Width, depthFrame.Height);
            //    gameMaskTexture.SetData(maskImageColors);

            //}

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

        Model pinModel;
        Matrix pinModelMatrix;

        //Texture2D pinTexture;
        Rectangle pinRectangle;
        Color pinColor = Color.Red;

        public float PinX, PinY, PinZ;
        public Vector3 PinVector;
        OnEventRecognized
        JointType pinJoint = JointType.HandRight;

        void updatePin()
        {
            if (activeSkeletonNumber == 0)
            {
                PinX = -10;
                PinY = -10;
                PinZ = -10;
            }
            else
            {
                Joint joint = activeSkeleton.Joints[pinJoint];
                DepthImagePoint depthImagePoint = myKinect.MapSkeletonPointToDepth(joint.Position, DepthImageFormat.Resolution640x480Fps30);

                ColorImagePoint pinPoint = myKinect.MapSkeletonPointToColor(
                    joint.Position,
                    ColorImageFormat.RgbResolution640x480Fps30);

                var position = GameWorldTransformation(joint.Position);
                //PinX = PinX+1;
                //PinY = 1;
                //PinX = depthImagePoint.X;
                //PinY = depthImagePoint.Y;
                PinX = position.X;
                PinY = position.Y;
                PinZ = -position.Z;
            }

            PinVector.X = PinX;
            PinVector.Y = PinY;
            PinVector.Z = PinZ;

            //pinRectangle.X = PinX - pinRectangle.Width / 2;
            //pinRectangle.Y = PinY - pinRectangle.Height / 2;
        }

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        public CloudGame()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";

            //load pose and gesture files
            BTC.LoadPoses("Poses.pbtc");
            BTC.LoadGestures("Gestures.gbtc");
            this.graphics.IsFullScreen = false;

            // Make the screen the same size as the video display output
            graphics.PreferredBackBufferWidth = 640;
            graphics.PreferredBackBufferHeight = 480;
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            base.Initialize();
            pinRectangle = new Rectangle(0, 0, GraphicsDevice.Viewport.Width / 20, GraphicsDevice.Viewport.Width / 20);

            fullScreenRectangle = new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);


        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            Model model = Content.Load<Model>("BeachBall");

            pinModel = Content.Load<Model>("sword");

            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);

            Texture2D cloudTexture = Content.Load<Texture2D>("Cloud");

            SoundEffect cloudPop = Content.Load<SoundEffect>("Pop");

            //pinTexture = Content.Load<Texture2D>("pin");

            messageFont = Content.Load<SpriteFont>("MessageFont");

            lineDot = Content.Load<Texture2D>("whiteDot");

            gameImageTexture = Content.Load<Texture2D>("CloudGameBackground");

            maskImageColors = new Color[gameImageTexture.Width * gameImageTexture.Height];

            primitives.Add(new SpherePrimitive(GraphicsDevice));
            primitives.Add(new CylinderPrimitive(GraphicsDevice, 2.0f, 0.6f, 32));
            primitives.Add(new CylinderPrimitive(GraphicsDevice, 10, 0.5f, 32));

            try
            {
                BTC.Start();
                setupKinect();
            }
            catch (InvalidOperationException)
            {
                System.Console.Write("Runtime initialization failed. Please make sure Kinect device is plugged in.");
            }
            
            SetupSpeechRecognition();

            for (int i = 0; i < noOfSprites; i++)
            {
                Vector3 position = //new Vector3(0, 0, 0);
                    new Vector3(-50,
                                rand.Next(-50, 50),
                                5);

                // Parallax scrolling of clouds
                Vector3 speed = new Vector3(i / 1000f, 0, 0);

                Cloud c = new Cloud(cloudTexture, model, position, speed, cloudPop);

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
            try
            {
                BTC.Stop();
            }
            catch
            {
            }
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

            //updatePin();

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

            projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4,
                                                    GraphicsDevice.Viewport.AspectRatio,
                                                    1.0f,
                                                    100);

            spriteBatch.Begin();

            if (kinectVideoTexture != null)
                spriteBatch.Draw(kinectVideoTexture, fullScreenRectangle, Color.White);

            //if (gameMaskTexture != null)
            //    spriteBatch.Draw(gameMaskTexture, fullScreenRectangle, Color.White);

            //if (activeSkeleton != null)
            //{
            //    drawSkeleton(activeSkeleton, Color.White);
            //}

            //spriteBatch.Draw(pinTexture, pinRectangle, pinColor);

            if (errorMessage.Length > 0)
            {
                spriteBatch.DrawString(messageFont, errorMessage, Vector2.Zero, Color.White);
            }

            spriteBatch.End();

            foreach (ISprite sprite in gameSprites)
                sprite.Draw(this);

            //Matrix world = Matrix.CreateTranslation(PinVector) * Matrix.CreateTranslation(0.0f, -1.0f, 0.0f);
            //DrawModel(pinModel, world , view, projection);

            // Draw the current primitive.
            GeometricPrimitive currentPrimitive = primitives[currentPrimitiveIndex];
            Color color = Color.SpringGreen;
            DrawPrimitveSkeleton(currentPrimitive, view, projection, color);
            if (startcastingRight) DrawPrimitveStartSword(primitives[1], view, projection, BTCHelper.Hand.Right, Color.WhiteSmoke);
            if (startcastingLeft) DrawPrimitveStartSword(primitives[1], view, projection, BTCHelper.Hand.Left, Color.WhiteSmoke);
            if (swortLeft) DrawPrimitveSword(primitives[0], view, projection, BTCHelper.Hand.Left);
            if (swortRight) DrawPinSword(pinModel, view, projection, BTCHelper.Hand.Right);

            // Reset the fill mode renderstate.
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            base.Draw(gameTime);
        }

        /// <summary>
        /// Does the work of drawing a model, given specific world, view, and projection
        /// matrices.
        /// </summary>
        /// <param name="model">The model to draw</param>
        /// <param name="world">The transformation matrix to get the model in the right place in the world.</param>
        /// <param name="view">The transformation matrix to get the model in the right place, relative to the camera.</param>
        /// <param name="projection">The transformation matrix to project the model's points onto the screen correctly.</param>
        private void DrawModel(Model model, Matrix world, Matrix view, Matrix projection)
        {
            foreach (ModelMesh mesh in model.Meshes)
            {
                foreach (BasicEffect effect in mesh.Effects)
                {
                    effect.World = world;
                    effect.View = view;
                    effect.Projection = projection;
                    //effect.FogEnabled = true;
                    //effect.FogColor = Color.CornflowerBlue.ToVector3(); // For best results, ake this color whatever your background is.
                    //effect.FogStart = 9.75f;
                    //effect.FogEnd = 10.25f;
                    //effect.DiffuseColor = Color.Black.ToVector3();
                }

                mesh.Draw();
            }
        }

        private void DrawPrimitveSkeleton(GeometricPrimitive primitive, Matrix view, Matrix projection, Color color)
        {
            try
            {
                if (activeSkeleton != null)
                {
                    
                    if (activeSkeleton.TrackingState == SkeletonTrackingState.Tracked)
                    {
                        float maxDepth = -1;
                        float maxHeight = 1;
                        float maxWidth = 1;

                        foreach (Joint joint in activeSkeleton.Joints)
                        {
                            Vector3 position = GameWorldTransformation(joint.Position);
                            if (maxDepth < position.Z)
                                maxDepth = position.Z;
                            if (maxHeight < position.Y)
                                maxHeight = position.Y;
                            if (maxWidth < position.X)
                                maxWidth = position.X;

                            Matrix world = new Matrix();
                            world = Matrix.CreateTranslation((float)position.X, (float)position.Y, (float)position.Z);
                            primitive.Draw(world, view, projection * Matrix.CreateScale(1.0f), color);
                        }
                        animationDepth = (int)maxDepth;
                        animationHeight = (int)maxHeight;
                        animationWidth = (int)maxWidth;
                    }
                }
            }
            catch
            {

            }
        }

        private void DrawPrimitveSword(GeometricPrimitive primitive, Matrix view, Matrix projection, BTCHelper.Hand hand)
        {
            try
            {
                if (activeSkeleton != null)
                {
                    if (activeSkeleton.TrackingState == SkeletonTrackingState.Tracked)
                    {
                        Joint joint;
                        Color color = Color.Red; ;
                        if (hand == BTCHelper.Hand.Right) joint = activeSkeleton.Joints[JointType.HandRight];
                        else joint = activeSkeleton.Joints[JointType.HandLeft];
                        var position = GameWorldTransformation(joint.Position);
                        BTCVector mydirection = BTCHelper.GetHandDirection(hand, activeSkeleton, new BTCVector(-1.0f, 1.0f, 1.0f));
                        Vector3 direction = new Vector3(mydirection.X, mydirection.Y, mydirection.Z);
                        Matrix world = new Matrix();
                        for (int i = 1; i < 10; i++)
                        {

                            if (ii % 10 == 0)
                            {
                                Color temp = start;
                                start = nect;
                                nect = temp;
                                ii++;
                            }


                            if (i % 2 != 0) color = start;
                            else color = nect;
                            position = position + direction * float.Parse(i.ToString()) * 0.5f / 3.0f;
                            world = Matrix.CreateTranslation((float)position.X, (float)position.Y, (float)position.Z);
                            primitive.Draw(world, view, projection * Matrix.CreateScale(1f, 1f, 1f), color);
                            

                        }
                        ii++;

                    }
                }
            }
            catch
            {

            }
        }

        private void DrawPinSword(Model model, Matrix view, Matrix projection, BTCHelper.Hand hand)
        {
            try
            {
                if (activeSkeleton != null)
                {
                    if (activeSkeleton.TrackingState == SkeletonTrackingState.Tracked)
                    {
                        Joint joint;
                        Color color = Color.Red; ;
                        if (hand == BTCHelper.Hand.Right) joint = activeSkeleton.Joints[JointType.HandRight];
                        else joint = activeSkeleton.Joints[JointType.HandLeft];
                        var position = GameWorldTransformation(joint.Position);
                        BTCVector mydirection = BTCHelper.GetHandDirection(hand, activeSkeleton, new BTCVector(-1.0f, 1.0f, 1.0f));
                        Vector3 direction = new Vector3(mydirection.X, mydirection.Y, mydirection.Z);
                        
                        position = position + direction * 0.5f / 3.0f;
                        pinModelMatrix = Matrix.CreateTranslation((float)position.X, (float)position.Y, (float)position.Z);
                        pinModelMatrix = Matrix.CreateScale(.1f, .1f, .1f) * Matrix.CreateRotationY(MathHelper.Pi) * pinModelMatrix;

                        DrawModel(pinModel, pinModelMatrix/*world matrix*/, view, projection);
                        

                    }
                }
            }
            catch
            {

            }
        }

        private void DrawPrimitveStartSword(GeometricPrimitive primitive, Matrix view, Matrix projection, BTCHelper.Hand hand, Color color)
        {
            try
            {
                if (activeSkeleton != null)
                {
                    if (activeSkeleton.TrackingState == SkeletonTrackingState.Tracked)
                    {
                        Joint joint;
                        if (hand == BTCHelper.Hand.Right)
                            joint = activeSkeleton.Joints[JointType.HandRight];
                        else joint = activeSkeleton.Joints[JointType.HandLeft];
                        var position = GameWorldTransformation(joint.Position);
                        Matrix world = new Matrix();
                        world = Matrix.CreateTranslation((float)position.X, (float)position.Y, (float)position.Z) * Matrix.CreateTranslation(0.0f, -1.0f, 0.0f);
                        primitive.Draw(world, view, projection * Matrix.CreateScale(1.0f), color);

                    }
                }
            }
            catch
            {

            }
        }

        /// <summary>
        /// Convert a  kinect point to game world point 
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        private Vector3 GameWorldTransformation(SkeletonPoint position)
        {
            var returnVector = new Vector3();
            returnVector.X = -position.X * 10;
            returnVector.Y = position.Y * 10;
            returnVector.Z = position.Z;
            return returnVector;
        }

        #region Event Handler

        void KinectNui_AllFrameReady(object sender, AllFramesReadyEventArgs e)
        {

            SkeletonFrame skeletonFrame = e.OpenSkeletonFrame();
            if (skeletonFrame == null) return;
            Skeleton[] skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
            skeletonFrame.CopySkeletonDataTo(skeletons);
            activeSkeleton = (from s in skeletons
                        where s.TrackingState == SkeletonTrackingState.Tracked
                        select s).FirstOrDefault();
        }

        void Game_OnEventRecognized(EventRecognizedEventArgs gca)
        {
            foreach (KinectEvent Event in gca.Events)
            {
                if (Event.Type == KEvents.Gesture)
                {
                    // Activate Right hand swort
                    if (Event.Value == "s11") swortRight = true;
                    // Activate Left hand swort
                    if (Event.Value == "sl11") swortLeft = true;

                }
                if (Event.Type == KEvents.Pose)
                {

                    if (Event.Value == "s1")
                    {
                        //Show Right swort casting animation
                        startcastingRight = true;
                        //Hide swort if it was active
                        swortRight = false;
                    }

                    if (Event.Value == "sl1")
                    {
                        //Show Left swort casting animation
                        startcastingLeft = true;
                        //Hide swort if it was active
                        swortLeft = false;
                    }
                }
            }
        }
        void Game_OnEventStopRecognized(EventStopRecognizedEventArgs gca)
        {
            foreach (KinectEvent Event in gca.Events)
            {
                if (Event.Type == KEvents.Pose)
                {
                    //Stop showng Right swort casting animation
                    if ((Event.Value == "s1"))
                    {
                        startcastingRight = false;
                    }
                    //Stop showng Left swort casting animation
                    if ((Event.Value == "sl1"))
                    {
                        startcastingLeft = false;
                    }
                }

            }
        }
        #endregion
    }
}
