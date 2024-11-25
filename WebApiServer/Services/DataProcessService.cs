﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using WebApiServer.Data;
using WebApiServer.DTOs;
using WebApiServer.Models;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace WebApiServer.Services
{
    public interface IDataProcessService
    {
        public Task<IActionResult> MultiplyData(MultiplyDataDTO multiplyDatas);
        //public Task<IActionResult> SaveNewFolder(FileContent[] loadedFiles, string token, int idProject);
        public Task<IActionResult> AddProjectData(FileContent[] loadedFiles);
        public FolderDTO ProcessUploadedFolder(FileContent[] loadedFiles);
    }

    public class DataProcessService : IDataProcessService
    {
        private readonly ApiDbContext _context;
        
        public DataProcessService(ApiDbContext context)
        {
            _context = context;
        }


        //POTREBUJEM BEZ ULOZENIA DO DB
        public FolderDTO ProcessUploadedFolder(FileContent[] loadedFiles)
        {
            if (loadedFiles != null)
            {
                try
                {
                    List<FileDTO> files = new List<FileDTO>();
                    List<double> excitactionList = new List<double>();
                    bool excitacieNacitane = false;
                    for (int i = 0; i < loadedFiles.Length; i++) //nacitanie iba exitacii
                    {
                        FileContent file = loadedFiles[i];
                        string[] lines = file.CONTENT.Split('\n');
                        bool startReading = false;

                        foreach (var row in lines)
                        {
                            string line = row.Replace("\r", "");
                            int index = 0;
                            int lastNumOfRows = 0;

                            if (line != null)
                            {
                                if (startReading == true && !string.IsNullOrWhiteSpace(line))
                                {
                                    string[] words = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                                    if (double.TryParse(words[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double x))
                                    {
                                        if (!excitacieNacitane || (excitacieNacitane && !excitactionList.Contains(x)))
                                            excitactionList.Add(x);
                                    }
                                }

                                if (startReading == false && line == "#DATA")
                                    startReading = true;
                            }
                        }
                        excitacieNacitane = true;

                    }
                    excitactionList.Sort();

                    for (int i = 0; i < loadedFiles.Length; i++)
                    {
                        FileContent file = loadedFiles[i];
                        List<IntensityDTO> intensityList = new List<IntensityDTO>();
                        int spectrum = -1;
                        string pattern = @"\d+(?=(\.sp|sp\.sp))";     //cisla co su po m a pred . 
                        Match typeOfData = Regex.Match(file.FILENAME, pattern);
                        if (int.TryParse(typeOfData.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out int resultType))
                        {
                            spectrum = resultType;
                        }

                        string[] lines = file.CONTENT.Split('\n');
                        bool startReading = false;
                        foreach (var row in lines)
                        {
                            string line = row.Replace("\r", "");
                            if (line != null)
                            {
                                if (startReading == true && !string.IsNullOrWhiteSpace(line))
                                {
                                    string[] words = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries); //rozdelenie slov v riadku
                                    int index = -1;
                                    if (double.TryParse(words[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double x))
                                    {
                                        index = excitactionList.BinarySearch(x);
                                    }

                                    if (double.TryParse(words[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double result)) //skusam slovo dat na double
                                    {
                                        intensityList.Add(new IntensityDTO { EXCITATION = excitactionList[index], INTENSITY = result });
                                    }
                                }

                                if (startReading == false && line == "#DATA")
                                    startReading = true;
                            }

                        }
                        files.Add(new FileDTO
                        {
                            ID = -1,
                            FILENAME = loadedFiles[i].FILENAME,
                            SPECTRUM = spectrum,
                            INTENSITY = intensityList
                        });

                    }
                    files = files.OrderBy(file => file.SPECTRUM).ToList();

                    FolderDTO newFolder = new()
                    {
                        ID = -1,
                        FOLDERNAME = loadedFiles[0].FOLDERNAME,
                        EXCITATION = excitactionList,
                        DATA = files
                    };
                    return newFolder;
                }
                catch (Exception ex)
                {
                    return new FolderDTO();
                }
            }
            else
            {
                return null; // Odpoveď 400 Bad Request
            }
        }

        public async Task<IActionResult> MultiplyData(MultiplyDataDTO multiplyDatas)
        {
            if (multiplyDatas != null)
            {
                try
                {
                    List<List<LoadedData>> allData = new List<List<LoadedData>>();
                    int maxCount = -1;

                    for (int i = 0; i < multiplyDatas.IDS.Count; i++)
                    {
                        List<LoadedData> datas = _context.LoadedDatas.Where(item => item.IdFile == multiplyDatas.IDS[i]).ToList();
                        if (datas.Count > maxCount) maxCount = datas.Count;

                        allData.Add(datas);

                        foreach (var data in datas)
                        {
                            double multiplied = data.Intensity * multiplyDatas.FACTORS[i];
                            data.MultipliedIntensity = multiplied;
                        }
                    }
                    int idProfile = 1;
                    if (_context.ProfileDatas.Count() >= 1)
                    {
                        idProfile = _context.ProfileDatas
                        .OrderByDescending(obj => obj.IdProfileData)
                        .FirstOrDefault().IdProfileData + 1;
                    }

                    if (allData[0][0].MultipliedIntensity.HasValue)
                    {
                        List<ProfileData> profileData = _context.ProfileDatas.Where(item => item.IdFolder == multiplyDatas.IDFOLDER).ToList();
                        _context.ProfileDatas.RemoveRange(profileData);
                    }
                    //for (int i = 0; i< allData.Count;i++) //kazdy stlpec
                    //{
                    //    double maxIntensity = -1;

                    //    for (int j = 0; j < allData[i].Count; j++) //kazdy riadok
                    //    {
                    //        {
                    //            var local = allData[i][j].MultipliedIntensity;

                    //            if (allData[i][j].MultipliedIntensity != null && allData[i][j].MultipliedIntensity > maxIntensity)
                    //                maxIntensity = (double)allData[i][j].MultipliedIntensity;

                    //        }


                    //    }

                    //    //create profile 
                    //    ProfileData profile = new ProfileData
                    //    {
                    //        IdProfileData = idProfile,
                    //        IdFolder = multiplyDatas.IDFOLDER,
                    //        MaxIntensity = maxIntensity,
                    //        Excitation = multiplyDatas.EXCITATION
                    //    };
                    //    _context.ProfileDatas.Add(profile);
                    //    idProfile++;

                    //}
                    double?[][] dataForExcitacion = new double?[allData.Count][];
                    for (int i = 0; i < allData.Count; i++) //kazdy stlpec
                    {
                        dataForExcitacion[i] = new double?[maxCount];
                        var datalist = allData[i];
                        int index = 0;
                        foreach (var excitacion in multiplyDatas.EXCITATION)
                        {
                            if (datalist.Count == 0) { 
                                continue;
                            };
                            var intensity = datalist.FirstOrDefault(i => i.Excitation == excitacion);
                            if (intensity != null) dataForExcitacion[i][index] = intensity.MultipliedIntensity;
                            else dataForExcitacion[i][index] = null;

                            index++;
                        }
                    }
                    for (int i = 0; i < dataForExcitacion[0].Length; i++) //kazdy riadok
                    {
                        double maxIntensity = -1;

                        for (int j = 0; j < dataForExcitacion.Length; j++) // každý folder
                        {

                            var local = dataForExcitacion[j][i].HasValue ? dataForExcitacion[j][i] : null;

                            if (local.HasValue && local > maxIntensity)
                            {
                                maxIntensity = local.Value;
                            }
                        }

                        ProfileData profile = new ProfileData
                        {
                            IdProfileData = idProfile,
                            Excitation = allData[0][i].Excitation,
                            IdFolder = multiplyDatas.IDFOLDER,
                            MaxIntensity = maxIntensity
                        };
                        _context.ProfileDatas.Add(profile);
                        idProfile++;
                    }

                    _context.SaveChanges();

                    return new OkResult(); // Odpoveď 200 OK
                }
                catch (Exception ex)
                {

                    return new BadRequestResult(); ;
                }
            }
            else
            {
                return new BadRequestResult(); // Odpoveď 400 Bad Request
            }
        }

        public async Task<IActionResult> AddProjectData(FileContent[] loadedFiles)
        //pridanie do existujueho proektu

        {
            if (loadedFiles != null)
            {
                try
                {
                    int idProject = loadedFiles[0].IDPROJECT;
                    int idFolder = 1;
                    if (_context.LoadedFolders.Count() >= 1)
                    {
                        idFolder = _context.LoadedFolders.OrderByDescending(obj => obj.IdFolder)
                         .FirstOrDefault().IdFolder + 1;
                    }
                    LoadedFolder newFolder = new LoadedFolder
                    {
                        FolderName = loadedFiles[0].FOLDERNAME,
                        IdFolder = idFolder,
                        IdProject = idProject
                    };
                    _context.LoadedFolders.Add(newFolder);
                    int idData = 1;
                    if (_context.LoadedDatas.Count() >= 1)
                    {
                        idData = _context.LoadedDatas.OrderByDescending(obj => obj.IdData)
                         .FirstOrDefault().IdData;
                    }
                    int rowCount = 1;

                    int idFile = 1;
                    if (_context.Projects.Count() >= 1)
                    {
                        idFile = _context.LoadedFiles.OrderByDescending(obj => obj.IdFile)
                        .FirstOrDefault().IdFile + 1;
                    }
                    for (int i = 0; i < loadedFiles.Length; i++)
                    {
                        FileContent file = loadedFiles[i];
                        int spectrum = -1;
                        string pattern = @"\d+(?=(\.sp|sp\.sp))"; //cisla co su po m a pred . 
                        Match typeOfData = Regex.Match(file.FILENAME, pattern);
                        if (int.TryParse(typeOfData.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out int resultType))
                        {
                            spectrum = resultType;
                        }

                        LoadedFile newFile = new LoadedFile
                        {
                            FileName = loadedFiles[i].FILENAME,
                            IdFolder = idFolder,
                            IdFile = idFile + i,
                            Spectrum = spectrum
                        };
                        _context.LoadedFiles.Add(newFile);

                        string[] lines = file.CONTENT.Split('\n');
                        bool startReading = false;


                        foreach (var row in lines)
                        {
                            string line = row.Replace("\r", "");


                            double excitacion = -1;
                            double data = -1;
                            if (line != null)
                            {
                                if (startReading == true && !string.IsNullOrWhiteSpace(line))
                                {
                                    string[] words = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries); //rozdelenie slov v riadku
                                    if (double.TryParse(words[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double x))
                                    {
                                        excitacion = x;
                                    }

                                    if (double.TryParse(words[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double result)) //skusam slovo dat na double
                                    {
                                        data = result;
                                    }


                                    LoadedData newRow = new LoadedData
                                    {
                                        IdFile = idFile + i,
                                        Excitation = excitacion,
                                        Intensity = data,
                                        IdData = idData + rowCount
                                    };
                                    rowCount++;
                                    _context.LoadedDatas.Add(newRow);

                                }
                                if (startReading == false && line == "#DATA") startReading = true;
                            }

                        }

                    }
                    _context.SaveChanges();
                    return new OkResult(); // Odpoveď 200 OK
                }
                catch (Exception ex)
                {
                    return new BadRequestResult(); ;
                }

            }
            else
            {
                return new BadRequestResult(); // Odpoveď 400 Bad Request
            }

        }


    }
}
