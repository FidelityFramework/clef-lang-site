---
title: "A Window Layout System for Fidelity"
linkTitle: "Window Layout System"
description: "Taking Lessons from WPF and Avalonia"
date: 2025-02-02
authors: ["Houston Haynes"]
tags: ["Design"]
params:
  originally_published: 2025-02-02
  original_url: "https://speakez.tech/blog/window-layout-with-fidelity/"
  migration_date: 2026-03-12
---

Our Fidelity framework builds desktop applications with [our Clef language](https://clef-lang.com), targeting native user interfaces across multiple platforms while keeping the functional programming model. The layout system is one hard part of that: it has to stay functional and still give developers the panel, grid, and stacking primitives that established UI frameworks offer.

This article describes how Fidelity can draw ideas from modern functional UI frameworks to build a pure Clef implementation of a window layout system without external dependencies, relying on Clef native code and the integrated low level LVGL and Skia libraries. The layout system sits within [the broader FidelityUI model]({{< ref "fidelity-ui-model" >}}) the framework builds toward. We also examine how Fidelity is designed to address UI process prioritization through its compiler pipeline, which matters to developers familiar with platforms that use a modernized WPF design.

> **Note for .NET Developers**: Unlike .NET frameworks that use concepts of threads and thread pools, Fidelity's native compilation architecture uses a different approach to concurrency. Rather than thinking about "UI threads" and "background threads," Fidelity operates with specialized processes and task scheduling. This article will explain the conceptual transition from the threading model to Fidelity's process-based architecture.

## Key Insights from Modern Functional UI Frameworks

Modern functional UI frameworks like WPF, Uno, and MAUI (as well as functional wrappers like Avalonia.FuncUI and Elmish.Uno) provide several valuable insights for Fidelity's layout system:

1. **Functional DSL for UI Definition**: A domain-specific language that makes UI creation declarative and composable
2. **Virtual DOM for Efficient Updates**: A mechanism to minimize UI updates by comparing virtual representations 
3. **Type-Safe Attributes**: Strong typing for UI properties that catches errors at compile time
4. **MVU (Model-View-Update) Integration**: Clean separation of state, view, and update logic
5. **Component System**: Reusable, composable UI elements

Each of these can be adapted to Fidelity's pure Clef approach with LVGL and Skia integration.

## Core Layout Model for Fidelity

### Panel-Based Layout System

Similar to Avalonia.FuncUI and Elmish.Uno (which borrowed from WPF), Fidelity can implement a panel-based layout system with panels like:

```fsharp
// Core panels in Fidelity
type Panel =
    | Grid of GridProperties
    | StackPanel of StackPanelProperties
    | Canvas of CanvasProperties
    | DockPanel of DockPanelProperties
    | WrapPanel of WrapPanelProperties
```

Each panel would have specific layout properties and behavior. For example, a `Grid` would define rows and columns, while a `StackPanel` would stack elements vertically or horizontally.

### Layout Properties

Layout properties control how elements are positioned within panels:

```fsharp
// Example of layout properties for Grid
type GridProperties = {
    RowDefinitions: RowDefinition list
    ColumnDefinitions: ColumnDefinition list
}

type RowDefinition = 
    | Auto
    | Pixel of float
    | Star of float

type ColumnDefinition =
    | Auto
    | Pixel of float
    | Star of float

// Attached properties for Grid children
type GridChildProperties = {
    Row: int
    Column: int
    RowSpan: int
    ColumnSpan: int
}
```

These properties would be applied to controls to determine their position and size within the parent container.

## Pure Clef DSL for Layout

One of Avalonia.FuncUI's strengths is its DSL for defining UI elements. Fidelity aims to create a similar DSL, but with a pure Clef implementation that targets LVGL and Skia:

```fsharp
module Fidelity.UI

let window title content =
    Window.create [
        Window.title title
        Window.content content
    ]

let grid rows columns children =
    Grid.create [
        Grid.rowDefinitions rows
        Grid.columnDefinitions columns
        Grid.children children
    ]

let stackPanel orientation children =
    StackPanel.create [
        StackPanel.orientation orientation
        StackPanel.children children
    ]

// Usage example
let mainView = 
    window "Hello Fidelity" (
        grid 
            [ RowDefinition.Auto; RowDefinition.Star ] 
            [ ColumnDefinition.Star ] 
            [
                TextBlock.create [
                    Grid.row 0
                    TextBlock.text "Welcome to Fidelity"
                ]
                StackPanel.create [
                    Grid.row 1
                    StackPanel.orientation Orientation.Vertical
                    StackPanel.children [
                        Button.create [ Button.content "Click me" ]
                        TextBox.create [ TextBox.watermark "Enter text..." ]
                    ]
                ]
            ]
    )
```

### Process-Aware Attribute System

Fidelity's attribute system is designed to enhance the approach seen in modern functional UI frameworks with compile-time processing context:

```fsharp
type IAttr<'t> = 
    interface
        abstract ProcessContext: ProcessContext  
    end

module AttrBuilder =
    let createProperty<'value> name value processContext = 
        { new IAttr<'t> with
            member _.ProcessContext = processContext }
        
    // For events - always require UI process
    let createEvent name handler =
        { new IAttr<'t> with
            member _.ProcessContext = ProcessContext.UIProcess }

module Button =
    let create attrs = ViewBuilder.Create<Button>(attrs)
    
    // Property can be set from any process
    let content value = 
        AttrBuilder.createProperty "Content" value ProcessContext.AnyProcess
    
    // Event handlers must be UI process
    let onClick handler =
        AttrBuilder.createEvent "Click" handler
```

This process-aware attribute system is designed to let the compiler verify processing safety at compile time, removing the need for runtime checks while keeping a familiar programming model for developers coming from traditional .NET and Fable UI frameworks.

## Compiler Integration and Process Coordination

Traditional UI frameworks handle thread marshaling at runtime. Fidelity is designed to address process coordination through its compilation pipeline instead, which would remove runtime overhead while coordinating UI operations and background tasks by construction.

### Compiler-Aware Process Boundaries

The Fidelity compiler is designed to analyze code to detect UI operations versus background processing:

```fsharp
// The compiler recognizes UI operations that must happen in the LVGL process
let view model dispatch =
    window "Example" (
        grid [ Auto; Star ] [ Star ] [
            button "Load Data" [ onClick (fun _ -> dispatch LoadData) ]
            textBlock $"Items: {model.Items.Length}"
        ]
    )

// The compiler identifies background processing
let loadData = coldStream {
    let! data = fetchFromDatabase()
    // Process boundary crossing detected here - compiler inserts coordination
    do! Timeline.dispatchOnUI (DataLoaded data)
}
```

For developers familiar with traditional WPF-style frameworks, this approach parallels how dispatcher mechanisms work, but instead of runtime checks, the Fidelity compiler statically analyzes the code and inserts appropriate process coordination where needed.

### Layout System and Process Context

During compilation, the layout description is transformed into platform-specific LVGL code with automatic process context awareness:

```fsharp
// High-level layout code written by developer
let mainView = 
    window "Hello Fidelity" (
        grid [ Auto; Star ] [ Star ] [
            TextBlock.create [ Grid.row 0; text "Welcome" ]
            Button.create [ Grid.row 1; content "Click"; onClick handleClick ]
        ]
    )

// Simplified representation of compiler-generated LVGL code
let compiledMainView() =
    // Process context ensured automatically
    LVGL.ensureUIProcess()
    
    // Layout instantiation with platform-specific optimizations
    let container = lv_obj_create(null)
    let grid = lv_obj_create(container)
    lv_obj_set_layout(grid, LV_LAYOUT_GRID)
    
    // Row and column setup
    lv_grid_set_row_dsc(grid, [| LV_GRID_CONTENT; LV_GRID_FR(1) |])
    lv_grid_set_col_dsc(grid, [| LV_GRID_FR(1) |])
    
    // Elements with automatic process-safety
    let btn = lv_btn_create(grid)
    lv_obj_add_event_cb(btn, compileHandleClick, LV_EVENT_CLICKED, null)
```

This compilation approach provides several advantages over runtime process coordination:

1. **Zero Runtime Overhead**: No need to check process context at runtime
2. **Compile-Time Safety**: Process boundary violations are caught during compilation
3. **Platform-Specific Optimization**: Process coordination adapts to target platform capabilities

### Platform-Adaptive Process Models

The compiler generates different process coordination code based on the target platform:

```fsharp
// Generated code for cooperative task scheduling (embedded systems)
// Process safety through task scheduling
let dispatchToUIProcess action =
    LVGL.Task.scheduleOnNextTick action

// Generated code for preemptive processing (desktop systems)
// Process safety through message passing
let dispatchToUIProcess action =
    if Process.currentId() = uiProcessId then
        action()
    else
        uiProcessQueue.enqueue action
        uiProcessEvent.signal()
```

This would allow the same source code to work efficiently across the entire computing spectrum while maintaining proper process coordination.

## Virtual DOM with Process Awareness

Many modern functional UI frameworks employ a virtual DOM approach to minimize UI updates. Fidelity aims to implement a similar system, but with process-awareness built into the diffing and patching process:

```fsharp
// Virtual DOM types with process context awareness
type IView = interface
    abstract ViewType: Type
    abstract Attrs: IAttr list
    abstract ProcessContext: ProcessContext  // Process requirements
end

type ProcessContext =
    | UIProcess      // Must run in UI process
    | AnyProcess     // Can run in any process
    | WorkerProcess  // Should run in background process

// Process-aware differ to detect changes
module VirtualDom =
    let diff (oldView: IView option) (newView: IView option) =
        // Compare old and new views to generate minimal updates
        // Also track process context of each change
        
    let patch (control: Control) (diffs: Diff list) =
        // Apply minimal updates to actual UI elements
        // Ensure each update runs in the appropriate process
        
    let updateRoot (host: Control, oldView: IView option, newView: IView option) =
        // Compiler inserts process context verification
        LVGL.ensureUIProcess()
        
        let diffs = diff oldView newView
        patch host diffs
```

Where traditional virtual DOM implementations check process context at runtime, our Fidelity virtual DOM is designed to carry process awareness at compile time, which would remove the need for those runtime checks. The update mechanism stays similar, with the process analysis moved into compilation.

## MVU Integration with Process-Aware Compiler

The Elmish Model-View-Update (MVU) pattern in Fidelity could benefit from compiler-assisted process coordination. Where the runtime approach is common in many WPF-inspired frameworks, our Fidelity compiler would establish process safety statically:

```fsharp
// traditional runtime process safety, handled in a dispatcher
let runWithSyncDispatch (arg: 'arg) (program : Program<'arg, 'model, 'msg, #IView>) = 
    // Syncs view changes from non-UI processes through a dispatcher
    let syncDispatch (dispatch: Dispatch<'msg>) (msg: 'msg) =
        if Dispatcher.UIThread.CheckAccess() // Runtime thread check
        then dispatch msg
        else Dispatcher.UIThread.Post (fun () -> dispatch msg)
    
    Program.runWithDispatch syncDispatch arg program

// In Fidelity, the compiler automatically inserts process coordination
// without runtime overhead
let elmishApp = 
    { init = init
      update = update  // Compiler analyzes update function
      view = view }    // View always runs in the UI process
```

This provides a familiar programming model but with improved performance and safety characteristics. The compiler is designed to ensure:

1. **View Runs in UI Process**: All view code runs in the LVGL process
2. **Update Process Safety**: Background operations in update are properly coordinated
3. **Command Process Context**: Commands return to the UI process when needed

### Timeline State Management with Process Awareness

Our Timeline reactive system is designed to extend the MVU pattern with built-in process awareness:

```fsharp
// Timeline signals automatically manage process context
let counterApp = elmishApp {
    // Model is a timeline signal
    let! model = Timeline.signal { Count = 0 }
    
    // Update function with background operations
    let update msg model =
        match msg with
        | Increment -> 
            { model with Count = model.Count + 1 }
        | LoadData -> 
            // Compiler detects background operation
            Frosty.start (
                coldStream {
                    let! data = fetchData()  // Background process
                    // Compiler inserts process coordination here
                    do! Timeline.dispatchOnUI (DataLoaded data)
                    return data
                }
            )
            model  // Return current model while loading
        | DataLoaded data ->
            // This handler always runs in UI process
            { model with Data = data }
    
    // View is always executed in UI process
    let view model dispatch =
        window "Counter" (
            stackPanel [ 
                textBlock $"{model.Count}"
                button "+" [ onClick (fun _ -> dispatch Increment) ]
                button "Load" [ onClick (fun _ -> dispatch LoadData) ]
            ]
        )
        
    return { Model = model; Update = update; View = view }
}
```

This approach provides several advantages over runtime dispatching mechanisms in traditional frameworks:

1. **No Runtime Process Checks**: Eliminates overhead of access checks
2. **No Accidental UI Process Violations**: Compile-time errors prevent process coordination mistakes
3. **Platform-Specific Optimization**: Process handling adapts to platform capabilities

## Implementing Layout Measurement and Arrangement

A critical aspect of any layout system is the measurement and arrangement of elements. Fidelity aims to implement this with process-aware pure Clef:

```fsharp
// Layout process with process awareness
type LayoutContext = {
    AvailableSize: Size
    ScaleFactor: float
    ProcessContext: ProcessContext  // Process context information
}

// ILayoutable interface for elements that participate in layout
type ILayoutable =
    abstract Measure: LayoutContext -> Size
    abstract Arrange: Rect -> unit
    abstract ProcessRequirement: ProcessContext  // Process requirement

// Example implementation for StackPanel with process awareness
type StackPanel() =
    // Process requirement - layout operations must run in UI process
    member this.ProcessRequirement = ProcessContext.UIProcess
    
    interface ILayoutable with
        member this.Measure context =
            // Compiler inserts process verification
            LVGL.ensureUIProcess()
            
            let mutable totalSize = Size(0, 0)
            
            for child in this.Children do
                let childSize = child.Measure(context)
                if this.Orientation = Orientation.Vertical then
                    totalSize.Width <- max totalSize.Width childSize.Width
                    totalSize.Height <- totalSize.Height + childSize.Height
                else
                    totalSize.Width <- totalSize.Width + childSize.Width
                    totalSize.Height <- max totalSize.Height childSize.Height
                    
            totalSize
            
        member this.Arrange rect =
            // Compiler inserts process verification
            LVGL.ensureUIProcess()
            
            let mutable offset = 0.0
            
            for child in this.Children do
                let childSize = child.DesiredSize
                let childRect =
                    if this.Orientation = Orientation.Vertical then
                        Rect(rect.X, rect.Y + offset, rect.Width, childSize.Height)
                    else
                        Rect(rect.X + offset, rect.Y, childSize.Width, rect.Height)
                        
                child.Arrange(childRect)
                
                if this.Orientation = Orientation.Vertical then
                    offset <- offset + childSize.Height
                else
                    offset <- offset + childSize.Width
                    
        member this.ProcessRequirement = ProcessContext.UIProcess
```

This process-aware layout system would ensure that all layout operations happen in the appropriate process, with compile-time verification.

## LVGL-Skia Rendering Integration

While many modern UI frameworks have their own rendering engines, Fidelity uses a hybrid approach with LVGL for UI components and Skia for custom rendering:

```fsharp
// Process-aware LVGL component wrapper
type LVGLControl = {
    Id: int
    Type: LVGLControlType
    Properties: Map<string, obj>
    Children: LVGLControl list
    ProcessContext: ProcessContext  // Process context information
}

// Process-aware converter from Fidelity view to LVGL
let toNativeLVGL (view: IView) : LVGLControl =
    // Compiler ensures this runs in the UI process
    LVGL.ensureUIProcess()
    
    // Convert Fidelity view to LVGL control structure
    
// Process-aware Skia renderer for custom drawing
type SkiaCanvas = {
    Surface: SkiaSurface
    DrawOperations: DrawOperation list
    ProcessContext: ProcessContext  // Process context information
}

// Shared buffer for LVGL and Skia with process coordination
let createSharedRenderBuffer width height =
    // Compiler ensures this runs in the UI process
    LVGL.ensureUIProcess()
    
    // Allocate buffer for both LVGL and Skia to use
    // with proper process coordination
```

This process-aware hybrid approach is designed to let Fidelity use both libraries while keeping process safety through compile-time verification.

## Conclusion

By drawing from modern UI frameworks while focusing on LVGL and Skia integration, Fidelity is designed to provide a pure Clef layout system that pairs Clef's functional model with the performance and native feel of platform-specific implementations.

The advantages of this approach include:

1. **Functional Purity**: A fully functional approach to UI development that fits naturally with Clef
2. **Cross-Platform Consistency**: The same programming model across all platforms
3. **Compile-Time Process Safety**: Process coordination violations caught at compile time rather than runtime
4. **Performance**: Direct compilation to native code with minimal process coordination overhead
5. **Type Safety**: Strong typing that catches errors at compile time
6. **Composability**: Reusable, composable components that follow functional programming principles

For developers familiar with traditional WPF-style frameworks and their functional variants, Fidelity is designed to keep a familiar programming model while moving process coordination to compile time. That coordination would happen during compilation rather than at runtime, removing the overhead of runtime checks while keeping the structural safety the compiler can verify.

> **From Threads to Processes: A Conceptual Bridge for .NET Developers**
> 
> For developers coming from .NET platforms like WPF, Uno, or MAUI, it's important to understand that Fidelity's native compilation architecture operates differently from .NET's threading model. In .NET, UI operations must happen on a specific UI thread, with background work delegated to thread pools.
> 
> In Fidelity's native compiled architecture:
> 
> - Instead of "threads," Fidelity deals with specialized processes and tasks
> - Rather than thread marshaling, Fidelity uses process coordination and message passing
> - The compiler, not the runtime, enforces process boundaries
> - Async operations are handled through task scheduling rather than thread management
> 
> This conceptual shift provides significant performance advantages while maintaining the familiar programming model that .NET and Fable developers expect.

We want our Fidelity layout system to draw the ideas worth keeping from existing functional UI frameworks while staying a pure Clef implementation over libraries like LVGL and Skia, with process boundaries checked during compilation. That is the direction we will keep building toward as the rest of the framework comes into place.

## See also

- [The FidelityUI Model]({{< ref "fidelity-ui-model" >}}): the broader UI model this layout system sits within
- [The WREN Stack]({{< ref "wren-stack" >}}): the WebView UI substrate that sits beside this native layout path
- [Where Native Goes, Mobile Follows]({{< ref "where-native-goes-mobile-follows" >}}): how the native and WebView UI patterns are chosen per target
