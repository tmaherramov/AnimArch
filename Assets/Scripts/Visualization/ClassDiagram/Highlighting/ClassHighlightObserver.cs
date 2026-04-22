using System;
using System.Collections.Generic;
using UnityEngine;
using Visualisation.Animation;

namespace Visualization.ClassDiagram
{

public class ClassHighlightObserver : HighlightObserver
{
        public ClassHighlightObserver(HighlightSubject subject) : base(subject)
        {
        }

        public override void Update()
        {
            ClassHighlightSubject s = Subject as ClassHighlightSubject;
            Animation.Animation a = Animation.Animation.Instance;

            if (s.HighlightInt == 1)
            {
                a.HighlightClass(s.ClassName,true);
            }
            else if (s.HighlightInt == 0)
            {
                a.HighlightClass(s.ClassName, false);
            }
            
        }

}



}
