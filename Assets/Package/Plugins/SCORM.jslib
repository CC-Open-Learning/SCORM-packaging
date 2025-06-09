/*******************************************************************************
**
** JavaScript library to expose methods in scorm.js to ScormAPIWrapper.cs
**
*******************************************************************************/

var plugin = {
    wgldebugPrint: function(str)
    {
        DebugPrint(UTF8ToString(str));
    },

    wgldoIsScorm2004: function(objectname, callbackname, randomnumber)
    {
        doIsScorm2004(UTF8ToString(objectname), UTF8ToString(callbackname), randomnumber);
    },

    wgldoGetValue: function(identifier, objectname, callbackname, key)
    {
        doGetValue(UTF8ToString(identifier), UTF8ToString(objectname), UTF8ToString(callbackname), key);
    },

    wgldoSetValue: function(identifier, value, objectname, callbackname, randomnumber)
    {
        doSetValue(UTF8ToString(identifier), UTF8ToString(value), UTF8ToString(objectname), UTF8ToString(callbackname), randomnumber);
    },

    wgldoCommit: function()
    {
        doCommit();
    },

    wgldoTerminate: function()
    {
        doTerminate();
    }
};

mergeInto(LibraryManager.library, plugin);