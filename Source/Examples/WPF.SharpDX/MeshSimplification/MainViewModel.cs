﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MainViewModel.cs" company="Helix Toolkit">
//   Copyright (c) 2014 Helix Toolkit contributors
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace MeshSimplification
{
    using System;
    using System.Windows.Media.Animation;
    using System.Windows.Media.Imaging;
    using DemoCore;
    using HelixToolkit.Wpf.SharpDX;
    using SharpDX;
    using Media3D = System.Windows.Media.Media3D;
    using Point3D = System.Windows.Media.Media3D.Point3D;
    using Vector3D = System.Windows.Media.Media3D.Vector3D;
    using Transform3D = System.Windows.Media.Media3D.Transform3D;
    using Color = System.Windows.Media.Color;
    using Plane = SharpDX.Plane;
    using Vector3 = SharpDX.Vector3;
    using Colors = System.Windows.Media.Colors;
    using Color4 = SharpDX.Color4;
    using System.Collections.Generic;
    using System.Linq;
    using SharpDX.Direct3D11;
    using System.Windows;
    using System.Windows.Data;
    using HelixToolkit.Wpf.SharpDX.Extensions;
    using System.Windows.Input;
    using System.Threading.Tasks;
    using System.Diagnostics;
    using HelixToolkit.Wpf.SharpDX.Assimp;
    using HelixToolkit.Wpf.SharpDX.Model.Scene;
    using System.Windows.Threading;

    public class MainViewModel : BaseViewModel
    {
        private Dispatcher dispatcher; 
        public event Action OnChangedModel;
        public string Name { get; set; }
        public MainViewModel ViewModel { get { return this; } }

        private List<string> models;

        public List<string> Models
        {
            get { return models; }
            set { models = value; OnPropertyChanged(); }
        }

        private string selectedModel;

        public string SelectedModel
        {
            get { return selectedModel; }
            set
            {
                selectedModel = value;
                OnPropertyChanged();
                AssimpLoad(value);
            }
        }


        private MeshGeometry3D model;
        public MeshGeometry3D Model
        {
            get { return model; }
            private set
            {
                if (SetValue(ref model, value) && model != null)
                {
                    NumberOfTriangles = model.Indices.Count / 3;
                    NumberOfVertices = model.Positions.Count;
                }
            }
        }

        public PhongMaterial ModelMaterial { get; set; }
        public PhongMaterial LightModelMaterial { get; set; }

        public Transform3D ModelTransform { private set; get; }

        public Vector3D Light1Direction { get; set; }
        public Color Light1Color { get; set; }
        public Color AmbientLightColor { get; set; }
        private Vector3D camLookDir = new Vector3D(-100, -100, -100);
        public Vector3D CamLookDir
        {
            set
            {
                if (camLookDir != value)
                {
                    camLookDir = value;
                    OnPropertyChanged();
                    Light1Direction = value;
                }
            }
            get
            {
                return camLookDir;
            }
        }

        public ICommand SimplifyCommand { private set; get; }
        public ICommand ResetCommand { private set; get; }

        private MeshSimplification simHelper;

        public bool Busy { set; get; } = false;

        private bool showWireframe = true;
        public bool ShowWireframe
        {
            set
            {
                if (SetValue(ref showWireframe, value))
                {
                    FillMode = value ? FillMode.Wireframe : FillMode.Solid;
                }
            }
            get
            {
                return showWireframe;
            }
        }

        public FillMode FillMode { set; get; } = FillMode.Wireframe;

        public int NumberOfTriangles { set; get; } = 0;
        public int NumberOfVertices { set; get; } = 0;

        private MeshGeometry3D OrgMesh;

        public bool Lossless { set; get; } = false;

        public long CalculationTime { set; get; } = 0;

        public MainViewModel()
        {
            dispatcher = Application.Current.Dispatcher;
            EffectsManager = new DefaultEffectsManager();

            // ----------------------------------------------
            // titles
            this.Title = "Mesh Simplification Demo";
            this.SubTitle = "WPF & SharpDX";

            // ----------------------------------------------
            // camera setup
            this.Camera = new PerspectiveCamera { Position = new Point3D(100, 100, 100), LookDirection = new Vector3D(-100, -100, -100), UpDirection = new Vector3D(0, 1, 0) };
            // ----------------------------------------------
            // setup scene
            this.AmbientLightColor = Colors.DimGray;
            this.Light1Color = Colors.Gray;


            this.Light1Direction = new Vector3D(-100, -100, -100);
            SetupCameraBindings(Camera);
            // ----------------------------------------------
            // ----------------------------------------------
            // scene model3d
            this.ModelMaterial = PhongMaterials.Silver;

            Models = new List<string> { "lens.stl", "bottle.stl", "wall12.obj", "suzanne.obj" };
            SelectedModel = Models[0];

            //var models = Load3ds("wall12.obj").Select(x => x.Geometry as MeshGeometry3D).ToArray();
            //var scale = new Vector3(1f);

            //foreach (var item in caritems)
            //{
            //    for (int i = 0; i < item.Positions.Count; ++i)
            //    {
            //        item.Positions[i] = item.Positions[i] * scale;
            //    }

            //}
            //Model = models[0];
            //OrgMesh = Model;

            //ModelTransform = new Media3D.RotateTransform3D() { Rotation = new Media3D.AxisAngleRotation3D(new Vector3D(1, 0, 0), -90) };

            SimplifyCommand = new RelayCommand(Simplify, CanSimplify);
            ResetCommand = new RelayCommand((o) => { Model = OrgMesh; simHelper = new MeshSimplification(Model); }, CanSimplify);

        }
        public void AssimpLoad(string path)
        {
            Busy = true;
            Task.Factory.StartNew(() =>
            {
                var importer = new Importer();
                return importer.Load(path);

            }).ContinueWith(antecedent =>
            {
                var scene = antecedent.Result;
                if (scene != null)
                {
                    var node = scene.Root.Traverse()
                      .FirstOrDefault(geom => geom is MaterialGeometryNode);

                    Model = ((MaterialGeometryNode)node).Geometry as MeshGeometry3D;
                    OrgMesh = Model;
                    simHelper = new MeshSimplification(Model);
                    dispatcher.BeginInvoke(new Action(()=> OnChangedModel?.Invoke()));
                }
                else
                {
                    MessageBox.Show("Model not loaded." + Environment.NewLine + "Not enough memory ?", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    Model = null;
                    OrgMesh = null;
                }
                Busy = false;
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
        public List<Object3D> Load3ds(string path)
        {
            var reader = new ObjReader();
            var list = reader.Read(path);
            return list;
        }
        public List<Object3D> LoadSTL(string path)
        {
            var reader = new StLReader();
            var list = reader.Read(path);
            return list;
        }

        public void SetupCameraBindings(Camera camera)
        {
            if (camera is ProjectionCamera)
            {
                SetBinding("CamLookDir", camera, ProjectionCamera.LookDirectionProperty, this);
            }
        }

        private static void SetBinding(string path, DependencyObject dobj, DependencyProperty property, object viewModel, BindingMode mode = BindingMode.TwoWay)
        {
            var binding = new Binding(path);
            binding.Source = viewModel;
            binding.Mode = mode;
            BindingOperations.SetBinding(dobj, property, binding);
        }

        private bool CanSimplify(object obj) { return !Busy; }
        private void Simplify(object obj)
        {
            if (!CanSimplify(null)) { return; }
            Busy = true;
            int size = Model.Indices.Count / 3 / 2;
            CalculationTime = 0;
            Task.Factory.StartNew(() =>
            {
                var sw = Stopwatch.StartNew();
                var model = simHelper.Simplify(size, 7, true, Lossless);
                sw.Stop();
                CalculationTime = sw.ElapsedMilliseconds;
                model.Normals = model.CalculateNormals();
                return model;
            }).ContinueWith(x =>
            {
                Busy = false;
                Model = x.Result;
                CommandManager.InvalidateRequerySuggested();
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
    }
}