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
            string validationMessage = ValidatePart(part);
            if (!string.IsNullOrEmpty(validationMessage))
            {
                return validationMessage;
            }

            part.UpdatedAt = DateTime.Now;
            _partRepository.Save(part);
            return SaveSuccessMessage;
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

            return string.Empty;
        }
    }
}

