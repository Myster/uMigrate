﻿using System;
using System.Collections.Generic;
using System.Linq;
using ClientDependency.Core;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Umbraco.Core.Models;
using uMigrate.Fluent;

namespace uMigrate.Internal.SyntaxImplementations {
    public class DataTypeSetSyntax : SetSyntaxBase<IDataTypeDefinition, IDataTypeSetSyntax, IDataTypeFilteredSetSyntax>, IDataTypeSetSyntax {
        public DataTypeSetSyntax([NotNull] IMigrationContext context, [CanBeNull] IReadOnlyList<IDataTypeDefinition> types = null)
            : base(context, () => types ?? context.Services.DataTypeService.GetAllDataTypeDefinitions().AsReadOnlyList()) 
        {
        }

        protected override IDataTypeSetSyntax NewSet(IEnumerable<IDataTypeDefinition> items) {
            return new DataTypeSetSyntax(Context, items.ToArray());
        }

        protected override string GetName(IDataTypeDefinition item) {
            return item.Name;
        }

        public IDataTypeSetSyntax Add(string name, string editorAlias, Guid? key) {
            Argument.NotNullOrEmpty("name", name);
            Argument.NotNullOrEmpty("editorAlias", editorAlias);

            var all = Services.DataTypeService.GetAllDataTypeDefinitions();
            var existing = all.SingleOrDefault(t => t.Name == name);
            if (existing != null) {
                Logger.Log("DataType: '{0}' already exists, skipping.", name);
                return NewSet(existing);
            }

            var dataType = new DataTypeDefinition(-1, editorAlias) { Name = name };
            if (key != null)
                dataType.Key = key.Value;

            Services.DataTypeService.Save(dataType);
            Logger.Log("DataType: added '{0}' (key '{1}'), using editor '{2}'.", name, key, editorAlias);
            return NewSet(dataType);
        }

        public IDataTypeSetSyntax SetPreValues(object preValues, bool overwrite = false) {
            Argument.NotNull("preValues", preValues);

            return ChangePreValues((collection, dataType) => {
                if (overwrite)
                    collection.PreValuesAsDictionary = new Dictionary<string, PreValue>();

                foreach (var pair in preValues.ToDictionary()) {
                    var value = pair.Value as string ?? JsonConvert.SerializeObject(pair.Value, Formatting.None);
                    SetPreValueInternal(dataType, collection, pair.Key, value);
                }
            });
        }

        public IDataTypeSetSyntax SetPreValue(string name, string value) {
            Argument.NotNullOrEmpty("name", name);
            Argument.NotNullOrEmpty("value", value);

            return ChangePreValues((preValues, dataType) => SetPreValueInternal(dataType, preValues, name, value));
        }

        private void SetPreValueInternal(IDataTypeDefinition dataType, PreValueCollection preValues, string name, string newValue) {
            var dictionary = preValues.FormatAsDictionary();
            var existing = dictionary.GetValueOrDefault(name);
            if (existing != null) {
                var oldValue = existing.Value;
                existing.Value = newValue;
                Logger.Log("DataType: '{0}', changed setting '{1}': '{2}' => '{3}'.", dataType.Name, name, oldValue, newValue);
                return;
            }

            preValues.PreValuesAsDictionary[name] = new PreValue(newValue);
            Logger.Log("DataType: '{0}', added setting '{1}': '{2}'.", dataType.Name, name, newValue);
        }

        public IDataTypeFilteredSetSyntax WhereEditorAliasIs(string alias) {
            Argument.NotNullOrEmpty("alias", alias);
            return Where(t => t.PropertyEditorAlias == alias);
        }

        public IDataTypeSetSyntax ChangePreValues(Action<PreValueCollection> change) {
            Argument.NotNull("change", change);
            return ChangePreValues((values, _) => change(values));
        }
        
        public IDataTypeSetSyntax ChangePreValues(Action<PreValueCollection, IDataTypeDefinition> change) {
            Argument.NotNull("change", change);

            return ChangeWithManualSave(dataType => {
                var preValues = Services.DataTypeService.GetPreValuesCollectionByDataTypeId(dataType.Id);
                change(preValues, dataType);

                Services.DataTypeService.SavePreValues(dataType.Id, preValues.PreValuesAsDictionary);
                Context.ClearCaches(); // nuclear option, but sometimes cache is incorrect if values are added quickly enough
            });
        }

        public IDataTypeSetSyntax ChangePreValue(string key, Action<PreValue> change) {
            Argument.NotNullOrEmpty("key", key);
            Argument.NotNull("change", change);

            return ChangeWithManualSave(dataType => {
                var preValues = Services.DataTypeService.GetPreValuesCollectionByDataTypeId(dataType.Id);
                var preValue = preValues.PreValuesAsDictionary.GetValueOrDefault(key);
                Ensure.That(
                    preValue != null,
                    "PreValue '{0}' not found on type '{1}'. Available keys: '{2}'.",
                    key, dataType.Name, string.Join("', '", preValues.PreValuesAsDictionary.Keys)
                );
                change(preValue);

                Services.DataTypeService.SavePreValues(dataType.Id, preValues.PreValuesAsDictionary);
            });
        }

        public IDataTypeSetSyntax ChangeAllPropertyValues<TFrom, TTo>(Func<TFrom, TTo> change) {
            Argument.NotNull("change", change);

            var dataTypeIds = new HashSet<int>(Objects.Select(t => t.Id));
            var allContentTypes = Services.ContentTypeService.GetAllContentTypes();
            allContentTypes.MigrateEach(contentType => {
                var relevantProperties = contentType.PropertyTypes.Where(p => dataTypeIds.Contains(p.DataTypeDefinitionId)).ToArray();
                if (!relevantProperties.Any())
                    return;

                ChangePropertyValues(contentType, relevantProperties, change);
            });
            return this;
        }

        public IDataTypeSetSyntax Delete() {
            foreach (var dataTypeDefinition in Objects){
                Services.DataTypeService.Delete(dataTypeDefinition);
                Logger.Log("DataType: deleted '{0}' (key '{1}').", 
                    dataTypeDefinition.Name, 
                    dataTypeDefinition.Key);
            }

            return this;
        }

        private void ChangePropertyValues<TFrom, TTo>(IContentType contentType, PropertyType[] properties, Func<TFrom, TTo> change) {
            var contents = Services.ContentService.GetContentOfContentType(contentType.Id);
            contents.MigrateEach(c => {
                properties.MigrateEach(p => {
                    var value = c.GetValue<TFrom>(p.Alias);
                    c.SetValue(p.Alias, change(value));
                });
                Services.ContentService.SaveThenPublishIfPublished(c);
            });
        }
    }
}
