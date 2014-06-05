﻿using System;
using System.Collections.Generic;

namespace Translator
{
    public class Delphi
    {
    	//Output
        public string name;        
        public Script script;
        
    	//Enums, consts, types
    	public Class classLocals, classGlobals;
        public List<Constant> localConsts, globalConsts;
    	public List<Enum> localEnums, globalEnums;
    	public List<Type> localTypes, globalTypes;
    	
    	public List<string> interfaceUses, implementationUses;
        
    	//Log
    	delegate void delegate_log(List<string> imessages);
    	delegate_log log; 
    	
        //Bookmarks
    	//Sections
    	private int startHeader, endHeader, startInterface, endInterface, startVar, endVar, startImplementation, endImplementation;    	
    	//Subsections
    	private List<int> startUses, endUses, startClassInterface, endClassInterface, startClassImplementation, endClassImplementation, startConsts, endConsts, startEnums, endEnums, startTypes, endTypes;
    	
    	
    	//Raw strings
    	//Header, class names, SubSection names, Uses interface, Uses implementation, (Global and Local): consts, enums, types and vars 
    	private List<string> classNames, SubSection_Names, Uses_Interface, Uses_Implementation, ConstsGlobal, EnumsGlobal, TypesGlobal, VarsGlobal, ConstsLocal, EnumsLocal, TypesLocal, VarsLocal;
    	//Raw text for the classes
    	private List<List<string>> classDefinitions, classImplementations;

    	
    	//Keywords
        //Divide script sections
        public static string[] sectionKeys = { "var", "implementation", "interface" };
    	//Divide section subsections
    	public static string[] subsectionKeys = { "type", "const", "uses", "class", "record", "class function", "class procedure", "procedure", "function"};
    	//Divide method commands
        public static string[] methodKeys = { "var", "begin", "label", "end", "try", "catch", "finally" };
    	

        List<int> Section_Bookmarks, EnumLocalStarts, EnumLocalEnds, EnumGlobalEnds;
        List<string> Section_Names;

    	//ireadheader is a flag if header is to be read. iheaderstart is normally "{ --" in Zinsser Delphi units, and iheaderend is "-- }" 
        public void Read(ref List<string> istrings, bool ireadheader, string iheaderstart, string iheaderend)
        {
        	//Initialise
            script = new Script();
            
            name = "";
            
            classNames = new List<string>();
            classDefinitions = new List<List<string>>();
            classImplementations = new List<List<string>>();
            
            ConstsGlobal = new List<string>();
            EnumsGlobal = new List<string>();
            TypesGlobal = new List<string>();
            VarsGlobal = new List<string>();            
            ConstsLocal = new List<string>();
            EnumsLocal = new List<string>();
            TypesLocal = new List<string>();
            VarsLocal = new List<string>();
            
            SubSection_Names = new List<string>();
            Section_Bookmarks = new List<int>();
            Section_Names = new List<string>();
 	
            startClassInterface = new List<int>();
            endClassInterface = new List<int>(); 
            startClassImplementation = new List<int>(); 
            endClassImplementation = new List<int>();            
            startUses = new List<int>();
            endUses = new List<int>();            
            startConsts = new List<int>();
            endConsts = new List<int>();             
            EnumLocalStarts = new List<int>();
            EnumLocalEnds = new List<int>();           
            startEnums = new List<int>();
            EnumGlobalEnds = new List<int>();           
            startTypes = new List<int>();
            endTypes = new List<int>();

            startHeader =  endHeader =  startInterface =  endInterface =  startImplementation =  endImplementation =  startInterface =  endInterface = -1;
            
            //Read unit name
            name = ((istrings[FindStringInList("unit", ref istrings, 0, true)].Replace(' ', ';')).Split(';'))[1];

            //Bookmark sections
            IndexStructure(ref istrings, ireadheader, iheaderstart, iheaderend);

            //Break up the text into Lists of strings for different parts
            if (ireadheader)
    			ParseHeader(ref istrings, ireadheader, iheaderstart, iheaderend);

			//Convert Delphi symbols to C# symbols
			Beautify(ref istrings);

            ParseInterface(ref istrings, ref Uses_Interface, ref classNames, ref classDefinitions, ref ConstsGlobal, ref EnumsGlobal, ref TypesGlobal);
            ParseVar(ref istrings, ref VarsGlobal);
            ParseImplementation(ref istrings, ref Uses_Implementation, ref classNames, ref classImplementations, ref ConstsLocal, ref EnumsLocal, ref TypesLocal);

            //Generate the text pieces into class objects
            GenerateGlobalClass(ref classGlobals, ref ConstsGlobal, ref EnumsGlobal, ref TypesGlobal, ref VarsGlobal);
            script.classes.Add(classGlobals);
            VarsGlobal.Clear();
            GenerateGlobalClass(ref classLocals, ref ConstsLocal, ref EnumsLocal, ref TypesLocal, ref VarsGlobal);
            script.classes.Add(classLocals);
            GenerateClasses(ref script.classes, ref classNames, ref classDefinitions, ref classImplementations);
            GenerateIncludes(ref interfaceUses, ref Uses_Interface);
            GenerateIncludes(ref implementationUses, ref Uses_Implementation);
            
            //Add both uses into one
            script.includes.AddRange(interfaceUses);
            script.includes.AddRange(implementationUses);
        }
                
        private void GenerateClasses(ref List<Class> oclasses, ref List<string> inames, ref List<List<string>> idefinitions, ref List<List<string>> iimplementations)
        {
        	//For each class discovered
        	for (int i=0; i< inames.Count; i++)
        	{
        		Class tclass;
        		List<string> tdefinition = idefinitions[i];
        		List<string> timplementation = iimplementations[i];
        		tclass.name = inames[i];
                Variable tvar;

        		//Add Variables, Properties and method names from definitions
        		for(int j=0; j < tdefinition.Count; j++)
        		{
        			//Contains all the parameters for each 
        			List<string> tdefintion_parts = RecognizeClassDefinition(tdefinition[i]);
        			switch(tdefintion_parts[0])
        			{
    					case "Variable":	//name, type
        									tvar = new Variable(tdefinition_parts[1], tdefinition_parts[2]);
    										tclass.variables.Add(tvar);
    										break;
    										
						case "Property":	//name, type, read, write
    										Property tprop = new Property(tdefinition_parts[1], tdefinition_parts[2], tdefinition_parts[3], tdefinition_parts[4]);
    										tclass.properties.Add(tprop);
    										break;
    					
						//This is for both functions and procedures. Difference is that type is returned "" for procedures    										
						case "Function":	List<Variable> tvars = new List<Variable>();
    										for (int ii=5; ii< tdefintion_parts.Count; )
    										{
    											tvar = new Variable(tdefinition_parts[ii], tdefinition_parts[ii+1]);
    											tvars.Add(tvar);
												ii += 2;    											
    										}
    										//name, type, IsVirtual, IsAbstract, IsStatic, Parameters
    										Function tfunc = new Function(tdefinition_parts[1], tdefinition_parts[2], ToBool(tdefinition_parts[3]), ToBool(tdefinition_parts[4]), ToBool(tdefinition_parts[5]), tvars);
    										tclass.functions.Add(tfunc);
    										break;
    										
						default:		    Log(tdefinition[j]);
											break;
        			}
        		}
        		
        		//Add Method implementation        		
        		for(int j=0; j < timplementation.Count; j++)
        		{
        			int k = FindNextFunctionImplementation(ref timplementation, j);
        			List<string> tfunctiontext = GetFunctionText(ref timplementation, j, k);
        			Function tfunction = FunctionFromText(ref tfunctiontext);
        			int l = FindFunctionInList(ref tclass.functions, tfunction);

        			//Add the function body and variables
    				tclass.functions[l].commands = tfunction.commands;
    				tclass.functions[l].variables = tfunction.variables;
        		}
        	}
        }
        
        private void GenerateGlobalClass(ref Class oclassGlobals, ref List<string> iconstsGlobal, ref List<string> ienumsGlobal, ref List<string> itypesGlobal, ref List<string> ivarsGlobal)
        {
        }

        private void GenerateIncludes(ref List<string> Objects_UsesInterface, ref List<string> Uses_Interface)
        {        
        }
        
        private void ParseHeader(ref List<string> istrings, bool ireadheader, string iheaderstart, string iheaderend)
        {
			 //Read Header
            if (ireadheader)
            	if (startHeader != -1)
	            	if (endHeader != -1)
	            		for (int i = startHeader; i < endHeader; i++)
	            			script.header.Add(istrings[i]);
	            	else
		            	throw new Exception("Header end not found");
        }
    	
        private void Beautify(ref List<string> istrings)
        {
        	for (int i = 0; i < istrings.Count; i++)
        	{
                Utilities.Beautify(istrings[i]);
        	}
        }
        
        //oclassdefinitions is a list of class variables, properties, function and procedure definitions 
        private void ParseInterface(ref List<string> istrings, ref List<string> ouses, ref List<string> oclassnames, ref List<List<string>> oclassdefinitions, ref List<string> oconsts, ref List<string> oenums, ref List<string> otypes )
        {                        
        	int tcurr_string_count = startInterface;
        	int tnext_subsection_pos = -1;
        	
            //uses
            tcurr_string_count = FindStringInList("uses", ref istrings, tcurr_string_count, true);
            if (tcurr_string_count != -1)
            {
	            tnext_subsection_pos = FindNextSubSection(ref istrings, tcurr_string_count);
	            
	            if (tnext_subsection_pos == -1)
	            	tnext_subsection_pos = endInterface;
	            
	            for (; tcurr_string_count < tnext_subsection_pos; tcurr_string_count++)
	            {
	            	ouses.Add(istrings[tcurr_string_count]);
	            }
            }
            else
        		tcurr_string_count = FindNextSubSection(ref istrings, 0);
            

            //Interface sub sections
            while (tcurr_string_count != -1)
            {
            	tnext_subsection_pos = FindNextSubSection(ref istrings, tcurr_string_count);
            	
            	switch (RecognizeKey(istrings[tcurr_string_count], ref subsectionKeys))
            	{
                    case "const":   oconsts.AddRange(GetStringSubList(ref istrings, tcurr_string_count + 1, tnext_subsection_pos)); break;
        			case "type": 	ParseInterfaceType(ref istrings, ref ouses, ref oclassnames, ref oclassdefinitions, ref oconsts, ref oenums, ref otypes, tcurr_string_count); break;
        			
        			//Log unrecognized sub section
        			default: 		List<string> tlogmessages = new List<string>();
        							tlogmessages.Add("Interface sub section not recognized " + tcurr_string_count);
        							tlogmessages.AddRange(GetStringSubList(ref istrings, tcurr_string_count, tnext_subsection_pos));
        							log(tlogmessages);
        							break;
            	}
            	tcurr_string_count = FindNextSubSection(ref istrings, tnext_subsection_pos);
            }        
        }
        
        private void ParseInterfaceType(ref List<string> istrings, ref List<string> ouses, ref List<string> oclassnames, ref List<List<string>> oclassdefinitions, ref List<string> oconsts, ref List<string> oenums, ref List<string> otypes, int tcurr_string_count)
        {
            int tnext_subsection_pos;
            //Interface sub sections
            while (tcurr_string_count < endInterface)
            {
            	tnext_subsection_pos = FindNextSubSection(ref istrings, tcurr_string_count);
            	switch (RecognizeKey(istrings[tcurr_string_count], ref subsectionKeys))
            	{
            			
            			 //{ "type", "const", "uses", "class", "record", "class function", "class procedure", "procedure", "function"};
            			
        			case "class": 	oclassdefinitions.Add(GetStringSubList(ref istrings, tcurr_string_count, tnext_subsection_pos)); break;
                    case "enum":    oenums.AddRange(GetStringSubList(ref istrings, tcurr_string_count, tnext_subsection_pos)); break;
                    case "type":    otypes.AddRange(GetStringSubList(ref istrings, tcurr_string_count, tnext_subsection_pos)); break;
        			
        			//Log unrecognized sub section
        			default: 		List<string> tlogmessages = new List<string>();
        							tlogmessages.Add("Interface sub section not recognized " + tcurr_string_count);
                                    tlogmessages.AddRange(GetStringSubList(ref istrings, tcurr_string_count, tnext_subsection_pos));
        							log(tlogmessages);
        							break;
            	}
            	tcurr_string_count = FindNextSubSection(ref istrings, tcurr_string_count);
            }
        }
        
        private void ParseVar(ref List<string> istrings, ref List<string> ovars)
        {
        	if (startVar != -1)
        		for (int i = startVar + 1; i < startImplementation; i++)
        			ovars.Add(istrings[i]);
        }

        //oclassimplementations is a list of class functions and procedures
        private void ParseImplementation(ref List<string> istrings, ref List<string> ouses, ref List<string> oclassnames, ref List<List<string>> oclassimplementations, ref List<string> oconst, ref List<string> oenum, ref List<string> oalias )
        {
        	int tcurr_string_count = startImplementation;
        	int tnext_subsection_pos = -1;
        	
            //uses
            tcurr_string_count = FindStringInList("uses", ref istrings, tcurr_string_count, true);
            if (tcurr_string_count != -1)
            {
	            tnext_subsection_pos = FindNextSubSection(ref istrings, tcurr_string_count);
	            
	            if (tnext_subsection_pos == -1)
	            	tnext_subsection_pos = endImplementation;
	            
	            for (; tcurr_string_count < tnext_subsection_pos; tcurr_string_count++)
	            {
	            	ouses.Add(istrings[tcurr_string_count]);
	            }
            }
            
            //The rest
            while (tcurr_string_count < endInterface)
            {
            	tcurr_string_count = FindNextSubSection(ref istrings, tcurr_string_count);
            	tnext_subsection_pos = FindNextSubSection(ref istrings, tcurr_string_count);
                switch (RecognizeKey(istrings[tcurr_string_count], ref sectionKeys))
            	{
                    case "Function":    oclassimplementations.Add(GetStringSubList(ref istrings, tcurr_string_count, tnext_subsection_pos)); break;
                    case "Procedure":   oclassimplementations.Add(GetStringSubList(ref istrings, tcurr_string_count, tnext_subsection_pos)); break;
        			case "Const": 		oconst.AddRange(GetStringSubList(ref istrings, tcurr_string_count, tnext_subsection_pos)); break;
                    case "Enum":        oenum.AddRange(GetStringSubList(ref istrings, tcurr_string_count, tnext_subsection_pos)); break;
                    case "Alias":       oalias.AddRange(GetStringSubList(ref istrings, tcurr_string_count, tnext_subsection_pos)); break;
					default: 			break;
            	}
            }
        }
  
    	private void IndexStructure(ref List<string> istrings, bool ireadheader, string iheaderstart, string iheaderend)
        {
            if (ireadheader)
            {
            	startHeader = FindStringInList(iheaderstart, ref istrings, 0, true);
            	if (startHeader != -1)
            	{
            		endHeader = FindStringInList(iheaderend, ref istrings, startHeader, true);
            		if (endHeader == -1)
		        		throw new Exception("Header end not found");

		        	Section_Names.Add("Header");
	        		Section_Bookmarks.Add(startHeader);
        		}
            }
            
            startInterface = FindStringInList("interface", ref istrings, 0, true);
        	if (startInterface == -1)
	    		throw new Exception("Interface not found");

        	Section_Names.Add("Interface");
    		Section_Bookmarks.Add(startInterface);

    		//If there is no other section, process
        	endInterface = FindNextSection(ref istrings, startInterface);
    		if (endInterface == -1)
        		return;
    		
            startImplementation = FindStringInList("implementation", ref istrings, endInterface, true);
    		if (startImplementation != -1)
    		{
    			//Look for a Var section in between interface and implementation
    			if (endInterface != startImplementation)
    			{
    				startVar = endInterface;
    				endVar = startImplementation;
    				
					Section_Names.Add("Var");
					Section_Bookmarks.Add(startVar);
    			}

    			Section_Names.Add("Implementation");
	    		Section_Bookmarks.Add(startImplementation);

	    		//If there is no other section after "implementation", then process
	    		endImplementation = FindNextSection(ref istrings, startImplementation);
	    		if (endImplementation == -1)
	    		{
	        		return;
	    		}
    		}
    		else
    		{
	            startVar = FindStringInList("var", ref istrings, endInterface, true);
	            if (startVar != -1)
	            {
					Section_Names.Add("Var");
					Section_Bookmarks.Add(startVar);
	            }
    		}
        }
        
        static private List<Class> StringsToClass(string[] istrings)
        {
            return new List<Class>();
        }      

        static private int GoToNextKeyword(string ifind, ref List<string> iarray, int iindex, bool imatchCase)
		{
            return 0;
		}

        static private int FindNextSection(ref List<string> istrings, int istartpos)
        {
            for (int i = istartpos; i < istrings.Count; i++)
            {
                if (CheckStringListElementsInString(istrings[i], ref sectionKeys) != -1)
                    return i;
            }
            return -1;
        }

        static private int FindNextSubSection(ref List<string> istrings, int istartpos)
        {
        	for (int i = istartpos; i < istrings.Count; i++)
        	{
        		if (CheckStringListElementsInString(istrings[i], ref subsectionKeys) != -1)
        			return i;
        	}
        	return -1;
        }

        static private string RecognizeKey(string istring, ref string[] ikeys)
		{
           	for (int i=0; i < ikeys.GetLength(0); i++)
           	{
           		if (istring.IndexOf(ikeys[i]) != -1)
           			return ikeys[i];
           	}
           	return "";
		}
        
        static private int CheckStringListElementsInString(string istring, ref string[] ielements)
		{
        	for (int i=0; i < ielements.GetLength(0); i++)
           	{
           		if (istring.IndexOf(ielements[i]) != -1)
           			return i;
           	}
           	return -1;
		}
                                                           
	    static private int FindStringInList(string ifind, ref List<string> iarray, int iindex, bool imatchCase)
		{
	    	for (int i = iindex; i < iarray.Count; i++)
	    	{
	    		string tstring;

                if (!imatchCase)
	    			tstring = iarray[i].ToLower();
	    		else
	    			tstring = iarray[i];
	    		
	    		if (tstring.IndexOf(ifind) != -1)
	    			return i;
	    	}
	    	return -1;
		}
	    
	    static private List<string> GetStringSubList(ref List<string> istrings, int istart, int iend)
	    {
	    	List<string> toutput = new List<string>();
	    	
	    	for (int i=istart; i < iend; i++)
	    		toutput.Add(istrings[i]);
	    	
	    	return toutput;
	    }
	}    
}
