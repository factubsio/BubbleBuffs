using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BubbleBuffs {

    public class SavedBufferState {

        [JsonProperty]
        public Dictionary<BuffKey, SavedBuffState> Buffs = new();
        [JsonProperty]
        public bool AllowInCombat;
        [JsonProperty]
        public int Version;
    }

   public class SavedCasterState {
        [JsonProperty]
        public bool Banned;
        [JsonProperty]
        public int Cap;
        [JsonProperty]
        public bool ShareTransmutation;
        [JsonProperty]
        public bool PowerfulChange;
    }

    public class SavedBuffState {

        [JsonProperty]
        public BuffGroup InGroup;
        [JsonProperty]
        public bool Blacklisted;
        [JsonProperty]
        public ISet<string> Wanted;
        [JsonProperty]
        public List<string> CasterPriority;
        [JsonProperty]
        public Dictionary<CasterKey, SavedCasterState> Casters = new();
        [JsonProperty]
        public Guid BaseSpell;
    }


}
