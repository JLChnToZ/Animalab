# Animalab

Animalab is a new programming language targets Unity's animator controller. It aims to give a flavour instead of dragging nodes, you may use any text editors to manipulate them.

## Installation

You can download this repository and drag `Assets/JLChnToZ/Animalab` folder into your project. To view the provided samples, you may need the additional `AnimatorDriver` folder as well.

## Getting Started

You can use `Assets > Create > Animalab Controller` to create a new controller, or doing so while select exists one to convert them into this format. Then you can open the created asset with any text editor you want to start editing.

This format is kind-of like ShaderLab, but with differences. The basic structure to declare a thing is like this:
```
type name expression(arguments...)... {
    contents...
};
```
Except type and the colon at the end, any other structures are not necessary exists for every types.

Also if the name, expressions and/or arguments you declare contains whitespace or symbols, you will have to use quotes (`'` or `"`) to surround them. Also, you can use C-style escapes (`\` followed by code) anytime if there are any conflicts to the syntax.

The following sections are about how to declare statements.

### Parameters
```
bool/int/float/trigger name = value;
```

### Layers & State Machine
```
layer name {
    default stateName;
    weight 0 ~ 1;
    ikPass;
    mask "path/to/avatar.mask";
    sync otherLayerName;
    syncTiming;

    transections...

    state / state machines...

    state machine behaviours...
}
```

State machines are similar to layers but it lacks weight, blending mode, IK pass, mask and sync statement, and the "layer" identifier change to "stateMachine".

### States
```
state name {
    time parameterName;
    speed parameterName;
    cycleOffset parameterName;
    mirror parameterName;
    ikOnFeet;
    writeDefaults;
    tag "tagName";

    clip / blend trees...

    state machine behaviours...
}
```

### Transections
```
[any] [noSelf] [muted] [solo] [wait((waitTime))] [if(if statement...)] [fade((fade time/fade time in seconds +s))] [source/destination/sourceThenDestination/destinationThenSource [ordered]] [goto otherState / end];
```

Where if statement:
```
numberParameter ==/!=/>/< value &&/|| boolOrTriggerToBeOn &&/|| !boolOrTriggerToBeOff
```

### Clip
```
clip "path/to/clip.anim" [* (timeScale in number)] [+/- (offset in number)];
```

### Blend trees
```
blendtree name [simple1D/simpleDirectional2D/freeformDirectional2D/reeformCartesian2D/direct ([parameterXName[, parameterYName]])] [threshold((min), (max))] {
    ((x), (y)): clip / blend trees...
    ...
} [* (timeScale in number)] [+/- (offset in number)];
```

### State Machine Behaviours
```
NameSpace.To.StateMachineType {
    parameter = value;
    arrayParameter = [
        value1;
        value2;
        ...
    ];
    arrayParameterWithStruct = [
        {
            nestedParameter = value;
            ...
        };
    ];
    structParameter = {
        nestedParameter = value;
        ...
    }
    ...
};
```

## License

[MIT](LICENSE)