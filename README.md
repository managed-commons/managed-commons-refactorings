# Commons.Refactorings 1.0.0-RC1
A Roslyn-based refactorings plugin for Visual Studio

**STILL BETA!!!**

Currently have three refactorings:

 1. __"Split here to a partial"__ refactoring that split the selected and following members from a class to a separate partial class inside the same source file.
 
 2. __"Break into partials"__ refactoring for classes with more than a certain number of members. Breaks into many partial classes inside the same source file.

 3. __"Move partial to new source file"__ refactoring that moves a partial class to another source, whose name is the current one appended with the first method's identifier or 'Partial' if no method is found inside the partial class.
