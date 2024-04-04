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
    public interface ILoadedDataService
    {
        public Task<IActionResult> MultiplyData(MultiplyDataDTO multiplyDatas);
        public Task<IActionResult> SaveNewFolder(FileContent[] loadedFiles, string token, int idProject);
        public Task<IActionResult> AddProjectData(FileContent[] loadedFiles);
        public FolderDTO ProcessUploadedFolder(FileContent[] loadedFiles);

        public string GenerateJwtToken();
    }

    public class LoadedDataService : ILoadedDataService
    {
        private readonly ApiDbContext _context;

        public LoadedDataService(ApiDbContext context)
        {
            _context = context;
        }

        public FolderDTO ProcessUploadedFolder(FileContent[] loadedFiles)
        {
            if (loadedFiles != null)
            {
                try
                { 
                   
                    int rowCount = 0;
                    List<FileDTO> files = new List<FileDTO>();
                    List<double> excitactionList = new List<double>();

                    for (int i = 0; i < loadedFiles.Length; i++)
                    {
                        FileContent file = loadedFiles[i];
                        List<double> intensityList = new List<double>();
                        int spectrum = -1;
                        string pattern = @"(?<=m)\d+(?=\.)"; //cisla co su po m a pred . 
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
                                    rowCount++;

                                    if (excitactionList.Count < rowCount)
                                        excitactionList.Add(excitacion);
                                    intensityList.Add(data);
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
                        }); ;

                    }

                    FolderDTO newFolder = new FolderDTO
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

                    for (int i = 0; i < multiplyDatas.IDS.Count; i++)
                    {
                        List<LoadedData> datas = _context.LoadedDatas.Where(item => item.IdFile == multiplyDatas.IDS[i]).ToList();

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
                        List<ProfileData> profileData =  _context.ProfileDatas.Where(item => item.IdFolder == multiplyDatas.IDFOLDER).ToList();
                        _context.ProfileDatas.RemoveRange(profileData);
                    }


                    for (int i = 0; i < allData[0].Count; i++) //kazdy riadok
                    {
                        double maxIntensity = -1;
                        for (int j = 0; j < allData.Count; j++) //kazdy folder
                        {
                            var local = allData[j][i].MultipliedIntensity;

                            if (allData[j][i].MultipliedIntensity > maxIntensity)
                                maxIntensity = (double)allData[j][i].MultipliedIntensity;

                        }

                        //create profile 
                        ProfileData profile = new ProfileData
                        {
                            IdProfileData = idProfile,
                            Excitation = allData[0][i].Excitation,
                            //IdFolder = multiplyDatas.IDFOLDER,
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

        public async Task<IActionResult> SaveNewFolder(FileContent[] loadedFiles, string token, int idProject)
        {
            if (loadedFiles != null)
            {
                try
                {
                    Project newProject = new Project
                    {
                        ProjectName = "NewProject",
                        IdProject = idProject,
                        Token = token,
                        Created = DateTime.UtcNow,
                    };
                    _context.Projects.Add(newProject);

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
                        string pattern = @"(?<=m)\d+(?=\.)"; //cisla co su po m a pred . 
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



                                if (startReading == false && line == "#DATA")
                                    startReading = true;
                            }

                        }

                    }
                    _context.SaveChanges();

                    return new OkResult(); // Odpoveď 200 OK
                }
                catch (Exception ex)
                {
                    return new BadRequestResult();
                }

            }
            else
            {
                return new BadRequestResult(); // Odpoveď 400 Bad Request
            }
        }

        public async Task<IActionResult> AddProjectData(FileContent[] loadedFiles)
        {
            if (loadedFiles != null)
            {
                try
                {
                    //int idProject = loadedFiles[0].IDPROJECT;
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
                        //IdProject = idProject
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
                        string pattern = @"(?<=m)\d+(?=\.)"; //cisla co su po m a pred . 
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



                                if (startReading == false && line == "#DATA")
                                    startReading = true;
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

        public string GenerateJwtToken()
        {
            // Generovanie JWT tokenu bez identifikátora užívate¾a
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes("L#9pD2m0oP7rW!4xN*1vL#9pD2m0oP7rW!4xN*1vL#9pD2m0oP7rW!4xN*1v");
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Expires = DateTime.UtcNow.AddDays(30),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

    }
}
