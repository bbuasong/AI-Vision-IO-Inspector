using System;
using System.Collections.Generic;
using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Application.Services
{
    /// <summary>
    /// 부품 기준정보 등록/조회/수정 업무를 담당합니다.
    /// UI는 저장소를 직접 호출하지 않고 이 서비스를 통해 업무 규칙을 거칩니다.
    /// </summary>
    public class PartCatalogService
    {
        public const string SaveSuccessMessage = "부품 기준정보가 저장되었습니다.";
        public const string DeleteSuccessMessage = "부품 기준정보가 삭제되었습니다.";
        public const string ReplaceAllSuccessMessage = "다중품목 기준정보가 DB에 저장되었습니다.";

        private readonly IPartRepository _partRepository;

        public PartCatalogService(IPartRepository partRepository)
        {
            _partRepository = partRepository;
        }

        public IList<Part> GetParts()
        {
            return _partRepository.GetAll();
        }

        public Part GetPart(string partNo)
        {
            return _partRepository.GetByPartNo(partNo);
        }

        public string SavePart(Part part)
        {
            string validationMessage = ValidatePartForSave(part);
            if (!string.IsNullOrEmpty(validationMessage))
            {
                return validationMessage;
            }

            part.UpdatedAt = DateTime.Now;
            _partRepository.Save(part);
            return SaveSuccessMessage;
        }

        /// <summary>
        /// 파일을 최종 이미지 폴더로 확정하기 전에 부품 저장 업무 규칙을 먼저 검사합니다.
        /// 파일 복사 후 분류코드 불일치로 DB 저장이 차단되는 순서 문제를 방지합니다.
        /// </summary>
        public string ValidatePartForSave(Part part)
        {
            return ValidatePart(part);
        }

        public string ReplaceAllParts(IList<Part> parts)
        {
            string validationMessage = ValidateParts(parts);
            if (!string.IsNullOrEmpty(validationMessage))
            {
                return validationMessage;
            }

            DateTime now = DateTime.Now;
            foreach (Part part in parts)
            {
                part.UpdatedAt = now;
            }

            _partRepository.ReplaceAll(parts);
            return ReplaceAllSuccessMessage;
        }

        public string DeletePart(string partNo)
        {
            if (string.IsNullOrWhiteSpace(partNo))
            {
                return "삭제할 Part No.가 없습니다.";
            }

            _partRepository.Delete(partNo);
            return DeleteSuccessMessage;
        }

        private string ValidatePart(Part part)
        {
            if (part == null)
            {
                return "저장할 부품 정보가 없습니다.";
            }

            if (string.IsNullOrWhiteSpace(part.PartNo))
            {
                return "Part No.를 입력해야 합니다.";
            }

            if (string.IsNullOrWhiteSpace(part.PartName))
            {
                return "Part Name을 입력해야 합니다.";
            }

            string categoryValidationMessage = ValidateCategoryConsistency(part);
            if (!string.IsNullOrEmpty(categoryValidationMessage))
            {
                return categoryValidationMessage;
            }

            return string.Empty;
        }

        private string ValidateParts(IList<Part> parts)
        {
            if (parts == null || parts.Count == 0)
            {
                return "저장할 다중품목 데이터가 없습니다.";
            }

            HashSet<string> partNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> categoryDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Part part in parts)
            {
                string validationMessage = ValidatePart(part);
                if (!string.IsNullOrEmpty(validationMessage))
                {
                    return validationMessage;
                }

                string partNo = part.PartNo.Trim();
                if (partNumbers.Contains(partNo))
                {
                    return "중복된 품번이 있습니다: " + partNo;
                }

                partNumbers.Add(partNo);

                string categoryCode = NormalizeCategoryCode(part.CategoryCode);
                if (string.IsNullOrWhiteSpace(categoryCode))
                {
                    continue;
                }

                string categoryDescription = NormalizeCategoryDescription(part.CategoryDescription);
                if (categoryDescriptions.ContainsKey(categoryCode))
                {
                    if (!string.Equals(categoryDescriptions[categoryCode], categoryDescription, StringComparison.OrdinalIgnoreCase))
                    {
                        return BuildCategoryMismatchMessage(categoryCode, categoryDescriptions[categoryCode], categoryDescription);
                    }
                }
                else
                {
                    categoryDescriptions[categoryCode] = categoryDescription;
                }
            }

            return string.Empty;
        }

        private string ValidateCategoryConsistency(Part part)
        {
            string categoryCode = NormalizeCategoryCode(part.CategoryCode);
            if (string.IsNullOrWhiteSpace(categoryCode))
            {
                return string.Empty;
            }

            string inputDescription = NormalizeCategoryDescription(part.CategoryDescription);
            string existingDescription = _partRepository.GetCategoryDescription(categoryCode);
            if (string.IsNullOrWhiteSpace(existingDescription))
            {
                return string.Empty;
            }

            existingDescription = NormalizeCategoryDescription(existingDescription);
            if (string.Equals(existingDescription, inputDescription, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return BuildCategoryMismatchMessage(categoryCode, existingDescription, inputDescription);
        }

        private string BuildCategoryMismatchMessage(string categoryCode, string existingDescription, string inputDescription)
        {
            return "분류코드 " + categoryCode + "은 이미 '" + existingDescription + "'으로 등록되어 있습니다.\r\n" +
                   "입력한 분류설명 '" + inputDescription + "'과 일치하지 않습니다.\r\n" +
                   "분류코드 또는 분류설명을 확인하세요.";
        }

        private string NormalizeCategoryCode(string categoryCode)
        {
            if (string.IsNullOrWhiteSpace(categoryCode))
            {
                return string.Empty;
            }

            return categoryCode.Trim();
        }

        private string NormalizeCategoryDescription(string categoryDescription)
        {
            if (string.IsNullOrWhiteSpace(categoryDescription))
            {
                return "-";
            }

            return categoryDescription.Trim();
        }
    }
}

