using System;
using System.Collections.Generic;
using AI.Vision.IOInspector.Application.Services;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.App.Stores
{
    /// <summary>
    /// 실행 중 사용하는 부품 기준정보 메모리 저장소입니다.
    /// SQLite는 영구 저장소로 두고, 화면 조회/검색은 이 캐시를 기준으로 처리합니다.
    /// </summary>
    public class PartDataStore
    {
        private readonly PartCatalogService _partCatalogService;
        private readonly IList<Part> _parts;

        public PartDataStore(PartCatalogService partCatalogService)
        {
            _partCatalogService = partCatalogService;
            _parts = new List<Part>();
        }

        public void LoadFromDatabase()
        {
            _parts.Clear();
            foreach (Part part in _partCatalogService.GetParts())
            {
                _parts.Add(part);
            }
        }

        public IList<Part> GetParts()
        {
            IList<Part> parts = new List<Part>();
            foreach (Part part in _parts)
            {
                parts.Add(part);
            }

            return parts;
        }

        public Part GetPart(string partNo)
        {
            if (string.IsNullOrWhiteSpace(partNo))
            {
                return null;
            }

            foreach (Part part in _parts)
            {
                if (string.Equals(part.PartNo, partNo.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return part;
                }
            }

            return null;
        }

        public IList<Part> SearchParts(PartSearchCriteria criteria)
        {
            IList<Part> parts = new List<Part>();
            foreach (Part part in _parts)
            {
                if (IsPartMatched(part, criteria))
                {
                    parts.Add(part);
                }
            }

            return parts;
        }

        public IList<string> BuildSearchSuggestions(PartSearchCriteria criteria, int maxCount)
        {
            IList<string> suggestions = new List<string>();
            if (criteria == null || maxCount <= 0)
            {
                return suggestions;
            }

            AddSuggestions(suggestions, criteria.GlobalKeyword, "PartNo", maxCount);
            AddSuggestions(suggestions, criteria.GlobalKeyword, "PartName", maxCount);
            AddSuggestions(suggestions, criteria.GlobalKeyword, "CategoryCode", maxCount);
            AddSuggestions(suggestions, criteria.GlobalKeyword, "CategoryDescription", maxCount);
            AddSuggestions(suggestions, criteria.GlobalKeyword, "PartType", maxCount);
            AddSuggestions(suggestions, criteria.PartNo, "PartNo", maxCount);
            AddSuggestions(suggestions, criteria.PartName, "PartName", maxCount);
            AddSuggestions(suggestions, criteria.CategoryCode, "CategoryCode", maxCount);
            AddSuggestions(suggestions, criteria.CategoryDescription, "CategoryDescription", maxCount);
            AddSuggestions(suggestions, criteria.PartType, "PartType", maxCount);
            return suggestions;
        }

        public IList<string> BuildFieldSearchSuggestions(PartSearchCriteria criteria, string fieldName, int maxCount)
        {
            IList<string> suggestions = new List<string>();
            if (criteria == null || maxCount <= 0 || string.IsNullOrWhiteSpace(fieldName))
            {
                return suggestions;
            }

            foreach (Part part in _parts)
            {
                if (suggestions.Count >= maxCount)
                {
                    return suggestions;
                }

                if (!IsPartMatched(part, criteria))
                {
                    continue;
                }

                string value = GetFieldValue(part, fieldName);
                if (!string.IsNullOrWhiteSpace(value) && !ContainsText(suggestions, value))
                {
                    suggestions.Add(value);
                }
            }

            return suggestions;
        }

        public string SavePart(Part part)
        {
            string message = _partCatalogService.SavePart(part);
            if (message == PartCatalogService.SaveSuccessMessage)
            {
                UpsertCache(part);
            }

            return message;
        }

        public string ReplaceAllParts(IList<Part> parts)
        {
            string message = _partCatalogService.ReplaceAllParts(parts);
            if (message == PartCatalogService.ReplaceAllSuccessMessage)
            {
                LoadFromDatabase();
            }

            return message;
        }

        public string DeletePart(string partNo)
        {
            string message = _partCatalogService.DeletePart(partNo);
            if (message == PartCatalogService.DeleteSuccessMessage)
            {
                RemoveFromCache(partNo);
            }

            return message;
        }

        private bool IsPartMatched(Part part, PartSearchCriteria criteria)
        {
            if (criteria == null)
            {
                return true;
            }

            if (!IsGlobalKeywordMatched(part, criteria.GlobalKeyword))
            {
                return false;
            }

            if (!ContainsKeyword(part.PartNo, criteria.PartNo))
            {
                return false;
            }

            if (!ContainsKeyword(part.PartName, criteria.PartName))
            {
                return false;
            }

            if (!ContainsKeyword(part.CategoryCode, criteria.CategoryCode))
            {
                return false;
            }

            if (!ContainsKeyword(part.CategoryDescription, criteria.CategoryDescription))
            {
                return false;
            }

            if (!ContainsKeyword(part.PartType, criteria.PartType))
            {
                return false;
            }

            return true;
        }

        private bool IsGlobalKeywordMatched(Part part, string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return true;
            }

            return ContainsKeyword(part.PartNo, keyword) ||
                   ContainsKeyword(part.PartName, keyword) ||
                   ContainsKeyword(part.CategoryCode, keyword) ||
                   ContainsKeyword(part.CategoryDescription, keyword) ||
                   ContainsKeyword(part.PartType, keyword);
        }

        private bool ContainsKeyword(string source, string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            return source.IndexOf(keyword.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void AddSuggestions(IList<string> suggestions, string keyword, string fieldName, int maxCount)
        {
            if (suggestions.Count >= maxCount || string.IsNullOrWhiteSpace(keyword))
            {
                return;
            }

            foreach (Part part in _parts)
            {
                if (suggestions.Count >= maxCount)
                {
                    return;
                }

                string value = GetFieldValue(part, fieldName);
                if (ContainsKeyword(value, keyword) && !ContainsText(suggestions, value))
                {
                    suggestions.Add(value);
                }
            }
        }

        private string GetFieldValue(Part part, string fieldName)
        {
            if (fieldName == "PartNo")
            {
                return part.PartNo;
            }

            if (fieldName == "PartName")
            {
                return part.PartName;
            }

            if (fieldName == "CategoryCode")
            {
                return part.CategoryCode;
            }

            if (fieldName == "PartType")
            {
                return part.PartType;
            }

            return part.CategoryDescription;
        }

        private bool ContainsText(IList<string> values, string text)
        {
            foreach (string value in values)
            {
                if (string.Equals(value, text, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void UpsertCache(Part part)
        {
            if (part == null || string.IsNullOrWhiteSpace(part.PartNo))
            {
                return;
            }

            for (int index = 0; index < _parts.Count; index++)
            {
                if (string.Equals(_parts[index].PartNo, part.PartNo, StringComparison.OrdinalIgnoreCase))
                {
                    _parts[index] = part;
                    return;
                }
            }

            _parts.Add(part);
        }

        private void RemoveFromCache(string partNo)
        {
            if (string.IsNullOrWhiteSpace(partNo))
            {
                return;
            }

            for (int index = _parts.Count - 1; index >= 0; index--)
            {
                if (string.Equals(_parts[index].PartNo, partNo.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    _parts.RemoveAt(index);
                }
            }
        }
    }
}
