using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace KinsenOfficial.Handlers
{
    /// <summary>
    /// Auto-assigns a unique incremental carID to each block item inside the 'carCardBlock' Block List
    /// when it's empty. Existing IDs remain untouched.
    /// </summary>
    public class CarCardAutoIncrementID : INotificationHandler<ContentSavingNotification>
    {
        private const string BlockListAlias = "carCardBlock"; // <-- βάλε εδώ το alias του property σου αν είναι διαφορετικό
        private const string CarIdAlias = "carId";            // <-- alias του πεδίου μέσα στο element

        public void Handle(ContentSavingNotification notification)
        {
            Debug.WriteLine("🚀 [CarCardAutoIncrementID] Handler triggered");

            foreach (var content in notification.SavedEntities)
            {
                Debug.WriteLine($"➡️ Node: {content.Name} (Id: {content.Id})");

                if (!content.HasProperty(BlockListAlias))
                {
                    Debug.WriteLine($"⛔ Property '{BlockListAlias}' not found on this content type.");
                    continue;
                }

                var raw = content.GetValue(BlockListAlias)?.ToString();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    Debug.WriteLine("⚠️ Block list value is null/empty.");
                    continue;
                }

                JObject root;
                try
                {
                    root = JObject.Parse(raw);
                }
                catch (JsonException ex)
                {
                    Debug.WriteLine($"❌ JSON parse error: {ex.Message}");
                    continue;
                }

                // Block List JSON structure: { "layout": {...}, "contentData": [ { "udi": "...", "contentTypeAlias": "...", "values": { ... } }, ... ] }
                var contentData = root["contentData"] as JArray;
                if (contentData == null)
                {
                    Debug.WriteLine("⛔ 'contentData' array not found in block list JSON.");
                    continue;
                }

                var usedIds = new HashSet<string>();
                bool modified = false;

                // Συλλογή υπαρχόντων IDs
                foreach (var cd in contentData)
                {
                    var values = cd["values"] as JObject;
                    var existing = values?.Value<string>(CarIdAlias);
                    if (!string.IsNullOrWhiteSpace(existing))
                    {
                        usedIds.Add(existing);
                    }
                }

                // Ανάθεση όπου είναι κενό
                foreach (var cd in contentData)
                {
                    var values = cd["values"] as JObject;
                    if (values == null) continue;

                    var carId = values.Value<string>(CarIdAlias);
                    if (string.IsNullOrWhiteSpace(carId))
                    {
                        var newId = GenerateUniqueId(usedIds);
                        values[CarIdAlias] = newId;
                        usedIds.Add(newId);
                        modified = true;

                        var maker = values.Value<string>("maker");
                        var model = values.Value<string>("model");
                        Debug.WriteLine($"🆕 Assigned carID={newId} for block (maker='{maker}', model='{model}')");
                    }
                    else
                    {
                        Debug.WriteLine($"✅ Keeping existing carID={carId}");
                    }
                }

                if (modified)
                {
                    var updated = root.ToString(Formatting.None);
                    content.SetValue(BlockListAlias, updated);
                    Debug.WriteLine("💾 Updated block list JSON saved back to the property.");
                }
                else
                {
                    Debug.WriteLine("ℹ️ No changes required. Skipping save.");
                }
            }

            Debug.WriteLine("🏁 [CarCardAutoIncrementID] Done.");
        }

        private string GenerateUniqueId(HashSet<string> existingIds)
        {
            int id = 1;
            while (existingIds.Contains(id.ToString()))
                id++;
            return id.ToString();
        }
    }
}
