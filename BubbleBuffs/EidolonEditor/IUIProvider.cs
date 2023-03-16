using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace BubbleBuffs.EidolonEditor {
    public interface IUIProvider {
        public string ID { get; }

        public void BuildUI(Transform root);

        public void Load();
        public void Unload();

        public Action<string> Log { set; }
    }
}
