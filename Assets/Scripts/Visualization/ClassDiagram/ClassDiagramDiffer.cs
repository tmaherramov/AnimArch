using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Visualization.ClassDiagram.ClassComponents;
using Visualization.ClassDiagram.Relations;
using OALProgramControl;
using System.Linq;
using System.Reflection;
using Visualization.ClassDiagram.MarkedDiagram;

namespace Visualization.ClassDiagram
{
    public class DiffResult
    {
        public CDRelationshipPoolMarked RelationshipPoolMarked { get; set; }
        public CDClassPoolMarked ClassPoolMarked { get; set; }

        public DiffResult()
        {
            this.RelationshipPoolMarked = new CDRelationshipPoolMarked();
            this.ClassPoolMarked = new CDClassPoolMarked();
        }
    }
    
    public class ClassDiagramDiffer
    {
        private OALProgram programInstance;
        private DiffResult diffResult;
        private ClassDiagramManager suggestedClassDiagramManager;
        
        private CDClassMarked currentClass;

        public static ClassDiagramDiffer CreateClassDiagramDifferWithCurrentDiagram()
        {
            ClassDiagramDiffer differ = new ClassDiagramDiffer();
            return differ;
        }
        
        public static ClassDiagramDiffer CreateClassDiagramDifferWithSpecifiedDiagram(OALProgram programInstance)
        {
            ClassDiagramDiffer differ = new ClassDiagramDiffer(programInstance);
            return differ;
        }

        private ClassDiagramDiffer(OALProgram programInstance)
        {
            this.programInstance = programInstance;
            this.diffResult = new DiffResult();
        }

        private ClassDiagramDiffer()
        {
            LoadCurrentDiagram();
            this.diffResult = new DiffResult();
        }

        private void LoadCurrentDiagram()
        {
            if (programInstance == null)
            {
                this.programInstance = Animation.Animation.Instance.CurrentProgramInstance;
            }
        }
        
        private bool RelationshipsAreEqual(CDRelationship a, CDRelationship b)
        {
            if (a.FromClass == b.ToClass)
            {
                return true;
            }
            return false;
        }

        private bool AreMethodsEqual(CDMethod a, CDMethod b)
        {
            return a.Name == b.Name;
        }
        
        private bool AreClassesEqual(CDClass a, CDClass b)
        {
            return a.Name == b.Name;
        }

        private static string NormalizeTypeNameForDiff(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return typeName;
            }

            string normalized = typeName.Trim();
            int arrayDepth = 0;
            while (normalized.EndsWith("[]", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(0, normalized.Length - 2).Trim();
                arrayDepth++;
            }

            normalized = EXETypes.ConvertEATypeName(normalized);

            if (arrayDepth == 0)
            {
                return normalized;
            }

            return normalized + string.Concat(Enumerable.Repeat("[]", arrayDepth));
        }

        private static bool AreAttributesEqual(CDAttribute a, CDAttribute b)
        {
            if (a == null || b == null)
            {
                return false;
            }

            // Tymur TODO HOTFIX:
            // Ignore attribute type changes for now.
            return string.Equals(a.Name, b.Name, StringComparison.Ordinal);
        }
        
        private List<MarkingDecorator<CDParameter>> MakeDifferenceMethodParameters(CDMethod a, CDMethod b)
        {
            List<MarkingDecorator<CDParameter>> changedParameters = new List<MarkingDecorator<CDParameter>>();
            
            List<CDParameter> oldParameters = a.Parameters;
            List<CDParameter> newParameters = b.Parameters;
            
            List<CDParameter> addedParameters = oldParameters.Where(p => !newParameters.Contains(p)).ToList();
            List<CDParameter> removedParameters = newParameters.Where(p => !oldParameters.Contains(p)).ToList();

            foreach (var addedParameter in addedParameters) 
            {
                MarkingDecorator<CDParameter> wrappedParameter = new MarkingDecorator<CDParameter>(addedParameter);
                wrappedParameter.SetCreateMark();
                changedParameters.Add(wrappedParameter);
            }
            
            foreach (var removedParameter in removedParameters) 
            {
                MarkingDecorator<CDParameter> wrappedParameter = new MarkingDecorator<CDParameter>(removedParameter);
                wrappedParameter.SetDeleteMark();
                changedParameters.Add(wrappedParameter);
            }
            
            return changedParameters;
        }

        private List<MarkingDecorator<CDAttribute>> MakeDifferenceAttributes(CDClass a, CDClass b)
        {
            List<MarkingDecorator<CDAttribute>> changedAttributes = new List<MarkingDecorator<CDAttribute>>();
            
            List<CDAttribute> oldAttributes = a.GetAttributes() ?? new List<CDAttribute>();
            List<CDAttribute> newAttributes = b.GetAttributes() ?? new List<CDAttribute>();
            
            List<CDAttribute> removedAttributes = oldAttributes
                .Where(oldAttribute => !newAttributes.Any(newAttribute => AreAttributesEqual(oldAttribute, newAttribute)))
                .ToList();
            
            List<CDAttribute> addedAttributes = newAttributes
                .Where(newAttribute => !oldAttributes.Any(oldAttribute => AreAttributesEqual(oldAttribute, newAttribute)))
                .ToList();

            foreach (var addedAttribute in addedAttributes) 
            {
                MarkingDecorator<CDAttribute> wrappedAttribute = new MarkingDecorator<CDAttribute>(addedAttribute);
                wrappedAttribute.SetCreateMark();
                changedAttributes.Add(wrappedAttribute);
            }
            
            foreach (var removedAttribute in removedAttributes) 
            {
                MarkingDecorator<CDAttribute> wrappedAttribute = new MarkingDecorator<CDAttribute>(removedAttribute);
                wrappedAttribute.SetDeleteMark();
                changedAttributes.Add(wrappedAttribute);
            }
            
            return changedAttributes;
        }

        private List<CDMethodMarked> MakeDifferenceMethods(CDClass a, CDClass b)
        {
            List<CDMethodMarked> differences = new List<CDMethodMarked>();
            
            List<CDMethod> oldMethodsList = a.GetMethods();
            List<CDMethod> newMethodsList = b.GetMethods();

            foreach (var oldMethod in oldMethodsList)
            {
                foreach (var newMethod in newMethodsList)
                {
                    if (oldMethod.Name != newMethod.Name) continue;
                    
                    List<MarkingDecorator<CDParameter>> changedParameters = MakeDifferenceMethodParameters(oldMethod, newMethod);
                    
                    if (changedParameters.Count != 0)
                    {
                        CDMethodMarked wrappedMethod = new CDMethodMarked(newMethod)
                        {
                            wrappedParameters = changedParameters
                        };
                        differences.Add(wrappedMethod);
                    }
                }
            }
            
            List<CDMethod> addedMethods = newMethodsList.Where(a => !oldMethodsList.Any(b => AreMethodsEqual(a, b))).ToList();
            List<CDMethod> removedMethods = oldMethodsList.Where(a => !newMethodsList.Any(b => AreMethodsEqual(a, b))).ToList();

            foreach (var addedMethod in addedMethods)
            {
                CDMethodMarked wrappedMethod = new CDMethodMarked(addedMethod);
                wrappedMethod.SetCreateMark();
                differences.Add(wrappedMethod);
            }
            
            foreach (var removedMethod in removedMethods)
            {
                CDMethodMarked wrappedMethod = new CDMethodMarked(removedMethod);
                wrappedMethod.SetDeleteMark();
                differences.Add(wrappedMethod);
            }
            
            return differences;
        }

        private void MakeDifferenceClasses()
        {
            List<CDClass> oldClassList = programInstance.ExecutionSpace.Classes;
            List<CDClass> newClassList = suggestedClassDiagramManager.cdClassPool.Classes;

            foreach (var oldCdClass in oldClassList)
            {
                foreach (var newCdClass in newClassList)
                {
                    if (oldCdClass.Name == newCdClass.Name)
                    {
                        currentClass = new CDClassMarked(oldCdClass);
                        bool isCurrentClassChanged = false;
                        
                        List<CDMethodMarked> methodsDifference = MakeDifferenceMethods(oldCdClass, newCdClass);
                        if (methodsDifference.Count != 0)
                        {
                            isCurrentClassChanged = true;
                            currentClass.WrappedMethods = methodsDifference;
                        }
                        
                        List<MarkingDecorator<CDAttribute>> attributesDifference = MakeDifferenceAttributes(oldCdClass, newCdClass);
                        if (attributesDifference.Count != 0)
                        {
                            isCurrentClassChanged = true;
                            currentClass.WrappedAttributes = attributesDifference;
                        }

                        if (isCurrentClassChanged)
                        {
                            diffResult.ClassPoolMarked.Add(currentClass);
                        }
                    }
                }
            }
            
            List<CDClass> addedClasses = newClassList.Where(a => !oldClassList.Any(b => AreClassesEqual(a, b))).ToList();
            List<CDClass> removedClasses = oldClassList.Where(a => !newClassList.Any(b => AreClassesEqual(a, b))).ToList();
            
            foreach (var addedClass in addedClasses)
            {
                CDClassMarked wrappedClass = new CDClassMarked(addedClass);
                wrappedClass.SetCreateMark();
                diffResult.ClassPoolMarked.Add(wrappedClass);
            }
            
            foreach (var removedClass in removedClasses)
            {
                CDClassMarked wrappedClass = new CDClassMarked(removedClass);
                wrappedClass.SetDeleteMark();
                diffResult.ClassPoolMarked.Add(wrappedClass);
            }
        }
        
        private void MakeDifferenceRelationships()
        {
            List<CDRelationship> oldList = programInstance.RelationshipSpace.GetAllRelationships();
            List<CDRelationship> newList = suggestedClassDiagramManager.cdRelationshipPool.GetAllRelationships();
            
            string GetKey(CDRelationship relationship) => $"{relationship.FromClass}::{relationship.ToClass}";
            
            Dictionary<string, CDRelationship> oldDict = oldList.ToDictionary(r => GetKey(r));
            Dictionary<string, CDRelationship> newDict = newList.ToDictionary(r => GetKey(r));

            var addedDict = newDict
                .Where(kv => !oldDict.ContainsKey(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            var removedDict = oldDict
                .Where(kv => !newDict.ContainsKey(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            foreach (var pair in addedDict)
            {
                var wrappedRelationship = new MarkingDecorator<CDRelationship>(pair.Value);
                wrappedRelationship.SetCreateMark();
                diffResult.RelationshipPoolMarked.Add(wrappedRelationship);
            }
            
            foreach (var pair in removedDict)
            {
                var wrappedRelationship = new MarkingDecorator<CDRelationship>(pair.Value);
                wrappedRelationship.SetDeleteMark();
                diffResult.RelationshipPoolMarked.Add(wrappedRelationship);
            }
        }

        public DiffResult GetDifference(ClassDiagramManager manager)
        {
            this.suggestedClassDiagramManager = manager;
            this.MakeDifferenceClasses();
            this.MakeDifferenceRelationships();
            return diffResult;
        }
    }
}
