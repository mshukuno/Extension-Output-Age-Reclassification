//  Copyright 2005-2010 Portland State University, University of Wisconsin
//  Authors:   Robert M. Scheller, James B. Domingo

using Landis.Utilities;
using Landis.Core;
using System.Collections.Generic;

namespace Landis.Extension.Output.AgeReclass
{
    /// <summary>
    /// A parser that reads the plug-in's parameters from text input.
    /// </summary>
    public class InputParametersParser
        : TextParser<IInputParameters>
    {
        //---------------------------------------------------------------------

        public InputParametersParser()
        {
        }
        public override string LandisDataValue
        {
            get
            {
                return PlugIn.ExtensionName;
            }
        }

        //---------------------------------------------------------------------

        protected override IInputParameters Parse()
        {

            //InputVar<string> landisData = new InputVar<string>("LandisData");
            //ReadVar(landisData);
            //if (landisData.Value.Actual != PlugIn.ExtensionName)
            //    throw new InputValueException(landisData.Value.String, "The value is not \"{0}\"", PlugIn.ExtensionName);
            ReadLandisDataVar();

            InputParameters parameters = new InputParameters(PlugIn.ModelCore.Species.Count);

            InputVar<int> timestep = new InputVar<int>("Timestep");
            ReadVar(timestep);
            parameters.Timestep = timestep.Value;

            // Table of reclass coefficients

            InputVar<string> speciesName = new InputVar<string>("Species");
            InputVar<double> reclassCoeff = new InputVar<double>("Reclass Coefficient");

            Dictionary <string, int> lineNumbers = new Dictionary<string, int>();

            const string ReclassMaps = "ReclassMaps";

            while (! AtEndOfInput && CurrentName != ReclassMaps) {
                StringReader currentLine = new StringReader(CurrentLine);

                ReadValue(speciesName, currentLine);
                ISpecies species = GetSpecies(speciesName.Value);
                CheckForRepeatedName(speciesName.Value, "species", lineNumbers);

                ReadValue(reclassCoeff, currentLine);
                parameters.ReclassCoefficients[species.Index] = reclassCoeff.Value;

                CheckNoDataAfter(string.Format("the {0} column", reclassCoeff.Name),
                                 currentLine);
                GetNextLine();
            }

            //  Read definitions of reclass maps

            ReadName(ReclassMaps);

            InputVar<string> mapName = new InputVar<string>("Map Name");
            InputVar<string> forestType = new InputVar<string>("Forest Type");

            lineNumbers.Clear();
            Dictionary <string, int> forestTypeLineNumbers = new Dictionary<string, int>();

            const string MapFileNames = "MapFileNames";
            const string nameDelimiter = "->";  // delimiter that separates map name and forest type

            IMapDefinition mapDefn = null;

            while (! AtEndOfInput && CurrentName != MapFileNames) {
                StringReader currentLine = new StringReader(CurrentLine);

                //  If the current line has the delimiter, then read the map
                //  name.
                if (CurrentLine.Contains(nameDelimiter)) {
                    ReadValue(mapName, currentLine);
                    CheckForRepeatedName(mapName.Value, "map name", lineNumbers);

                    mapDefn = new MapDefinition();
                    mapDefn.Name = mapName.Value;
                    parameters.ReclassMaps.Add(mapDefn);

                    TextReader.SkipWhitespace(currentLine);
                    string word = TextReader.ReadWord(currentLine);
                    if (word != nameDelimiter) {
                        throw NewParseException("Expected \"{0}\" after the map name {1}.",
                                                nameDelimiter, mapName.Value.String);
                    }

                    forestTypeLineNumbers.Clear();
                }
                else {
                    //  If there is no name delimiter and we don't have the
                    //  name for the first map yet, then it's an error.
                    if (mapDefn == null)
                        throw NewParseException("Expected a line with map name followed by \"{0}\"", nameDelimiter);
                }

                ReadValue(forestType, currentLine);
                CheckForRepeatedName(forestType.Value, "forest type",
                                     forestTypeLineNumbers);

                IForestType currentForestType = new ForestType(PlugIn.ModelCore.Species.Count);
                currentForestType.Name = forestType.Value;
                mapDefn.ForestTypes.Add(currentForestType);

                //  Read species for forest types

                List<string> speciesNames = new List<string>();

                TextReader.SkipWhitespace(currentLine);
                while (currentLine.Peek() != -1) {
                    ReadValue(speciesName, currentLine);
                    string name = speciesName.Value.Actual;
                    bool negativeMultiplier = name.StartsWith("-");
                    if (negativeMultiplier) {
                        name = name.Substring(1);
                        if (name.Length == 0)
                            throw new InputValueException(speciesName.Value.String,
                                "No species name after \"-\"");
                    }
                    ISpecies species = GetSpecies(new InputValue<string>(name, speciesName.Value.String));
                    if (speciesNames.Contains(species.Name))
                        throw NewParseException("The species {0} appears more than once.", species.Name);
                    speciesNames.Add(species.Name);

                    currentForestType[species.Index] = negativeMultiplier ? -1 : 1;

                    TextReader.SkipWhitespace(currentLine);
                }
                if (speciesNames.Count == 0)
                    throw NewParseException("At least one species is required.");

                GetNextLine();
            }

            // Template for filenames of reclass maps

            InputVar<string> mapFileNames = new InputVar<string>(MapFileNames);
            ReadVar(mapFileNames);
            parameters.MapFileNames = mapFileNames.Value;

            CheckNoDataAfter(string.Format("the {0} parameter", MapFileNames));

            return parameters; //.GetComplete();
        }

        //---------------------------------------------------------------------

        protected ISpecies GetSpecies(InputValue<string> name)
        {
            ISpecies species = PlugIn.ModelCore.Species[name.Actual];
            if (species == null)
                throw new InputValueException(name.String,
                                              "{0} is not a species name.",
                                              name.String);
            return species;
        }

        //---------------------------------------------------------------------

        private void CheckForRepeatedName(InputValue<string>      name,
                                          string                  description,
                                          Dictionary<string, int> lineNumbers)
        {
            int lineNumber;
            if (lineNumbers.TryGetValue(name.Actual, out lineNumber))
                throw new InputValueException(name.String,
                                              "The {0} {1} was previously used on line {2}",
                                              description, name.String, lineNumber);
            lineNumbers[name.Actual] = LineNumber;
        }
    }
}
