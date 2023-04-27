using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Common
{
    public class PaintShapeFactory
    {
        private static PaintShapeFactory _instance = null;
        private Dictionary<string, IShape> _prototypes = new Dictionary<string, IShape>();

        public static PaintShapeFactory Instance
        {
            get {
                if (_instance == null)
                {
                    _instance = new PaintShapeFactory();
                }
                return _instance; }
        }

        private PaintShapeFactory() {
            LoadShapePrototypes();
        }

        public IShape CreateShape(string name)
        {
            IShape shape = null;
            if (_prototypes.ContainsKey(name))
                shape = _prototypes[name].Clone();
            return (IShape) shape;
        }

        public void Reload()
        {
            _prototypes.Clear();
            LoadShapePrototypes();
        }

        private IShape? _getPaintShapeFromDll(FileInfo fileInfo)
        {
            Assembly assembly = Assembly.Load(AssemblyName.GetAssemblyName(fileInfo.FullName));
            //Assembly assembly = Assembly.LoadFile(fileInfo.FullName);
            Type[] types = assembly.GetTypes();

            IEnumerable<IShape?> results = types.Where(type =>
            {
                bool check = type.IsClass && typeof(IShape).IsAssignableFrom(type)
                                    && !typeof(Point2D).Equals(type);
                
                return type.IsClass && typeof(IShape).IsAssignableFrom(type)
                                    && !typeof(Point2D).Equals(type);
            })
             .Select(type => Activator.CreateInstance(type) as IShape);
            
            foreach (var result in results)
            {
                return result;
            }

            return null;
        }

        public void LoadShapePrototypes()
        {
            string exePath = Assembly.GetExecutingAssembly().Location;
            //string _folder = Path.GetDirectoryName(exePath) + "/shapes";
            string _folder = Path.GetDirectoryName(exePath);

            DirectoryInfo shapesDir = new DirectoryInfo(_folder);
            if (!shapesDir.Exists)
            {
                shapesDir.Create();
                return;
            }

            FileInfo[] fis = new DirectoryInfo(_folder).GetFiles("*.dll");
            foreach (FileInfo fileInfo in fis)
            {
                IShape? shape = _getPaintShapeFromDll(fileInfo);
                if (shape != null)
                {
                    Console.WriteLine(shape.Name);
                }

                if (shape != null && !_prototypes.ContainsKey(shape.Name))
                {
                    _prototypes.Add(shape.Name, shape);
                }
            }
        }

        public Dictionary<string, IShape> GetPrototypes()
        {
            return _prototypes;
        }

    }
}
