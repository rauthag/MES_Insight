using System;
using System.Collections.Generic;
using System.Linq;
using MESInsight.Core;

namespace MESInsight.Assembly
{
    public class AssyRelation
    {
        public string UidAssy { get; set; }
        public string UidAssyType { get; set; }
        public string ProcDir { get; set; }
        public DateTime Timestamp { get; set; }
        public ResponseRecord SourceRecord { get; set; }
    }

    public class AssemblyNode
    {
        public string Uid { get; set; }
        public string UidType { get; set; }
        public List<AssyRelation> Components { get; set; } = new List<AssyRelation>();
    }

    public class AssemblyIndex
    {
        private Dictionary<string, List<AssyRelation>> _uidToComponents
            = new Dictionary<string, List<AssyRelation>>(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, string> _uidTypes
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);


        public IReadOnlyDictionary<string, List<AssyRelation>> UidToComponents => _uidToComponents;

        public void Build(List<ResponseRecord> records)
        {
            _uidToComponents.Clear();
            _uidTypes.Clear();

            var semiRecords = records
                .Where(r => r.Type == MessageType.SEMI_VALIDATION2 || r.Type == MessageType.SEMI_VALIDATION)
                .Where(r => !string.IsNullOrEmpty(r.UidAssy))
                .OrderBy(r => r.TimestampParsed);

            foreach (var r in semiRecords)
            {
                string uid = r.UidIn ?? r.Uid;
                if (string.IsNullOrEmpty(uid)) continue;

                if (!_uidToComponents.TryGetValue(uid, out var list))
                    _uidToComponents[uid] = list = new List<AssyRelation>();

                list.Add(new AssyRelation
                {
                    UidAssy = r.UidAssy,
                    UidAssyType = r.UidAssyType ?? "",
                    ProcDir = r.ProcDirAssy ?? "?",
                    Timestamp = r.TimestampParsed,
                    SourceRecord = r
                });

                if (!string.IsNullOrEmpty(r.UidType))
                    _uidTypes[uid] = r.UidType;
            }
        }

        public AssemblyNode GetNode(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return null;
            _uidToComponents.TryGetValue(uid, out var components);
            _uidTypes.TryGetValue(uid, out var uidType);
            return new AssemblyNode
            {
                Uid = uid,
                UidType = uidType ?? "",
                Components = components ?? new List<AssyRelation>()
            };
        }

        public List<string> AllUids => _uidToComponents.Keys.OrderBy(k => k).ToList();
        public int TotalRelations => _uidToComponents.Values.Sum(v => v.Count);
        public bool IsBuilt => _uidToComponents.Count > 0;

        public List<string> GetRootUids()
        {
            var allChildren = new HashSet<string>(
                _uidToComponents.Values
                    .SelectMany(v => v.Select(r => r.UidAssy))
                    .Where(u => !string.IsNullOrEmpty(u)),
                StringComparer.OrdinalIgnoreCase);

            return _uidToComponents.Keys
                .Where(uid => !allChildren.Contains(uid))
                .OrderBy(k => k)
                .ToList();
        }

        public List<string> GetChildren(string uid)
        {
            if (!_uidToComponents.TryGetValue(uid, out var rels)) return new List<string>();
            return rels
                .Select(r => r.UidAssy)
                .Where(u => !string.IsNullOrEmpty(u))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}