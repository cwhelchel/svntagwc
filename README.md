## Project Description

SVNTAGWC will help users and configuration managers tag builds of their projects. It will automatically freeze all external revisions and add all unversioned files to a specified copy (or tag).

Optionally, if no action is taken, it will write the "frozen" externals to a file for each versioned folder with ``svn:externals``. The contents of the file can then be used to via ``svn propset --file F`` to freeze the externals or be used to ensure the proper action of the program.

*NOTE:* The subversion command line utility must be found on your path in order for svntagwc to work. That is you must be able to issue the same ``svn`` commands on your own at the command line. (I used the binaries from [CollabNet] (http://www.collab.net/downloads/subversion/).


## Links

*Special Thanks* to the developer of [SVNXF](http://svnxf.codeplex.com/) for letting me base this work off of his.

See the original Codeplex version [here] (http://svntagwc.codeplex.com/). I've moved it to Github just for the fun of it.