---
title: "Leveraging Fabulous for Native UI"
linkTitle: "Leveraging Fabulous for Native UI"
description: "How the Fabulous project guides FidelityUI design"
date: 2025-05-20
authors: ["Houston Haynes"]
tags: ["Design", "Innovation"]
params:
  originally_published: 2025-05-20
  original_url: "https://speakez.tech/blog/leveraging-fabulous-for-native-ui/"
  migration_date: 2026-03-12
---

Creating a native UI framework for [our Clef language](https://clef-lang.com) raises a specific question: how would we preserve the functional programming experience Clef developers work in while compiling to native code with deterministic memory management? As we envision FidelityUI, our UI framework for the Fidelity ecosystem, we sit at the intersection of functional programming and systems programming. We would not have to start from scratch. The Fabulous framework has already worked through many of the design challenges we would face, and by adapting its patterns to our native compilation context, we could build something that reads as familiar to Clef developers while delivering the performance required for embedded and real-time systems.

> **Note on Cross-Pollination**: While this article focuses on lessons from Fabulous, our FidelityUI would also draw on SwiftUI's declarative syntax and compile-time optimizations. Where Swift rests on an object-oriented foundation, FidelityUI would stay with Clef's functional roots and the MVU/Elmish patterns the Clef community already works in. Where SwiftUI uses property wrappers and 'combines', FidelityUI would use pure functions and algebraic data types, keeping the referential transparency that makes functional programs easier to reason over.

## Understanding the Translation Challenge

Fabulous provides a functional API for building user interfaces in F#, managing state through the Model-View-Update pattern, and updating the UI through its diffing algorithm. Our challenge would be to take these high-level patterns and compile them down to direct calls to LVGL (Light and Versatile Graphics Library) and Skia, removing runtime overhead in the process.

🔑 We wouldn't need to recreate Fabulous from scratch. Instead, we could adapt its core concepts while replacing its runtime infrastructure with compile-time transformations. Where Fabulous creates widget trees at runtime, FidelityUI would generate static LVGL object hierarchies at compile time. Where Fabulous uses heap-allocated closures for event handling, FidelityUI would transform these into direct function pointers suitable for embedded systems.

## The Widget Model: From Abstract to Concrete

Fabulous's widget model gives our FidelityUI a starting point. In Fabulous, widgets are lightweight descriptions of UI elements, structured so they can be compared and diffed. Here is how we could adapt this model for native compilation.

In Fabulous, a widget looks like this:

```fsharp
[<Struct>]
type Widget =
    { Key: WidgetKey
      ScalarAttributes: ScalarAttribute[]
      WidgetAttributes: WidgetAttribute[]
      WidgetCollectionAttributes: WidgetCollectionAttribute[]
      EnvironmentAttributes: EnvironmentAttribute[] }
```

For FidelityUI, we would keep this same conceptual structure, with one difference: these widgets would exist only at compile time. Our Composer compiler would transform them into direct LVGL calls. Here is how this transformation would work conceptually:

```fsharp
// What the developer writes (using Fabulous-like API)
let view model =
    VStack() {
        Label($"Temperature: {model.Temperature}°C")
        Button("Refresh", fun () -> dispatch Refresh)
        if model.IsLoading then
            Spinner()
    }

// What Composer generates (pseudo-code showing the concept)
let create_view model dispatch parent =
    let container = LVGL.obj_create parent
    LVGL.obj_set_layout container LV_LAYOUT_FLEX
    LVGL.obj_set_flex_flow container LV_FLEX_FLOW_COLUMN
    
    let label = LVGL.label_create container
    let text = sprintf "Temperature: %d°C" model.Temperature
    LVGL.label_set_text label text
    
    let button = LVGL.btn_create container
    let button_label = LVGL.label_create button
    LVGL.label_set_text button_label "Refresh"
    LVGL.obj_add_event_cb button refresh_handler LV_EVENT_CLICKED
    
    if model.IsLoading then
        let spinner = LVGL.spinner_create container 1000 60
    
    container
```

Notice how the high-level functional description would transform into low-level imperative calls. This transformation would happen entirely at compile time, guided by the patterns we've learned from Fabulous. Eventually this imperative code would be converted to "Oak AST" and then translated to MLIR instructions via XParsec. A library of composable elements from XParsec's *parser combinator* patterns would provide a full translation layer with lexical calls into the compiler lowering passes to produce the UI in a manner similar to SwiftUI.

## SwiftUI: Declarative Meets Functional

While Fabulous gives our FidelityUI its primary architectural reference, we also look to SwiftUI for lessons in declarative UI design. SwiftUI demonstrated that declarative UI could be performant in resource-constrained environments like watchOS. Where SwiftUI relies on Swift's reference types and ARC (Automatic Reference Counting), FidelityUI would target zero heap allocations.

Consider SwiftUI's view modifiers pattern:

```swift
// SwiftUI approach
Text("Hello")
    .foregroundColor(.blue)
    .font(.title)
    .padding()
```

Our FidelityUI would adapt this fluent interface style while compiling it to zero-cost abstractions:

```fsharp
// FidelityUI - looks similar but compiles very differently
Label("Hello")
    |> Label.textColor Color.Blue
    |> Label.fontSize FontSize.Title
    |> Label.padding (Thickness.uniform 10.0)
```

SwiftUI's modifiers create wrapper views at runtime, while our FidelityUI modifiers would be compile-time constructs that generate direct LVGL style calls. This would give us SwiftUI's API design without the runtime overhead.

## Compile-Time Resolution

One of Fabulous's features is its attribute system, which attaches properties to widgets in a type-safe manner. Our FidelityUI would adapt this pattern, but instead of storing attributes at runtime, we would resolve them during compilation.

The proposed transformation would look like this (FidelityUI not yet implemented):

```mlir
// Proposed MLIR for LVGL bindings
%parent_ref = memref.alloca() : memref<1x!lvgl.obj>
%label_ref = func.call @lv_label_create(%parent_ref) : (memref<1x!lvgl.obj>) -> memref<1x!lvgl.obj>

%text = memref.alloca() : memref<6xi8>  // "Hello\0"
func.call @lv_label_set_text(%label_ref, %text) : (memref<1x!lvgl.obj>, memref<?xi8>) -> ()

// Color becomes a direct style modification
%blue_color = arith.constant 0x0000FF : i32
func.call @lv_obj_set_style_text_color(%label_ref, %blue_color, %c0) : (memref<1x!lvgl.obj>, i32, i32) -> ()

// Font size becomes a style property
%font_size = arith.constant 16 : i32
func.call @lv_obj_set_style_text_font_size(%label_ref, %font_size, %c0) : (memref<1x!lvgl.obj>, i32, i32) -> ()
```

The vision for FidelityUI is that developers keep the familiar Fabulous-style API while the compiler lowers those high-level descriptions to native code.

## Layout Systems: From Functional to Imperative

Layout is perhaps where the translation from Fabulous to LVGL would become most interesting. Fabulous uses a functional approach to layout, with panels that measure and arrange their children. LVGL provides its own layout system with flexbox and grid layouts, which would map surprisingly well to Fabulous's concepts.

Let's examine how a grid layout would translate:

```fsharp
// Fabulous-inspired grid definition
Grid() {
    // Define rows and columns
    rows [ Auto; Star(1.0); Pixels(50.0) ]
    columns [ Star(1.0); Star(2.0) ]
    
    // Place children with attached properties
    Label("Title")
        |> Grid.row 0
        |> Grid.columnSpan 2
        
    TextBox(model.Value)
        |> Grid.row 1
        |> Grid.column 0
        
    Button("Submit", submit)
        |> Grid.row 1
        |> Grid.column 1
}
```

The Composer compiler would transform this into LVGL's grid layout API:

```fsharp
// Generated code (conceptual)
let create_grid parent =
    let grid = LVGL.obj_create parent
    
    // Set up grid layout
    LVGL.obj_set_layout grid LV_LAYOUT_GRID
    
    // Define row and column descriptors
    let row_dsc = [| LV_GRID_CONTENT; LV_GRID_FR(1); 50 |]
    let col_dsc = [| LV_GRID_FR(1); LV_GRID_FR(2) |]
    LVGL.obj_set_grid_dsc_array grid row_dsc col_dsc
    
    // Create and position children
    let title = LVGL.label_create grid
    LVGL.label_set_text title "Title"
    LVGL.obj_set_grid_cell title LV_GRID_ALIGN_STRETCH 0 2
                                  LV_GRID_ALIGN_STRETCH 0 1
    
    // ... similar for other children
```

As implied above, we would not implement a layout system from scratch. Instead, we would build a functional API layer that compiles down to LVGL's existing layout system, pairing a functional programming surface with a native implementation underneath.

## Event Handling Without Closures

Perhaps the most challenging aspect of adapting Fabulous patterns to native code would be event handling. Fabulous makes extensive use of closures to capture state and dispatch messages, but in our deterministic memory world, we would need a different approach. This is where we would diverge most significantly from both Fabulous and SwiftUI, which rely heavily on heap-allocated closures.

Consider a typical Fabulous event handler:

```fsharp
Button("Increment", fun () -> dispatch (Increment 1))
```

This creates a closure that captures both `dispatch` and the value `1`. In FidelityUI, we would transform this pattern using static function pointers and explicit context passing:

```fsharp
// What the developer writes (same as Fabulous)
Button("Increment", fun () -> dispatch (Increment 1))

// What Composer generates
// First, a static handler function
let button_click_handler (event: lv_event_t) =
    let user_data = LVGL.event_get_user_data event
    let context = NativePtr.read<EventContext> user_data
    context.dispatch (Increment 1)

// Then, the button creation with context
let create_button parent dispatch =
    let btn = LVGL.btn_create parent
    let context = { dispatch = dispatch; value = 1 }
    let context_ptr = NativePtr.stackalloc<EventContext> 1
    NativePtr.write context_ptr context
    LVGL.obj_add_event_cb btn button_click_handler LV_EVENT_CLICKED context_ptr
```

This transformation would remove the heap allocation while preserving the functional programming model at the source level. The developer would write idiomatic Clef code, and our Composer would transform it into native code.

## Compile-Time Optimization

One of Fabulous's features is its diffing algorithm, which updates only the parts of the UI that have changed. For our FidelityUI, we would adapt this concept with one shift: where possible, we would perform the diffing at compile time.

Consider a view function with conditional rendering:

```fsharp
let view model =
    VStack() {
        Label($"Status: {model.Status}")
        
        match model.State with
        | Loading -> Spinner()
        | Error msg -> ErrorPanel(msg)
        | Success data -> DataView(data)
    }
```

Instead of generating code that creates and destroys widgets at runtime, Composer could analyze the possible states and generate specialized update functions:

```fsharp

let update_view container old_model new_model =
    // Always update the label (it's dynamic)
    let label = LVGL.obj_get_child container 0
    LVGL.label_set_text label (sprintf "Status: %s" new_model.Status)
    
    // Handle state transitions efficiently
    match old_model.State, new_model.State with
    | Loading, Loading -> 
        () // No change needed
    
    | Loading, Error msg ->
        // Hide spinner, show error panel
        let spinner = LVGL.obj_get_child container 1
        LVGL.obj_add_flag spinner LV_OBJ_FLAG_HIDDEN
        let error_panel = create_error_panel container msg
        error_panels.[container] <- error_panel
    
    | Error _, Success data ->
        // Hide error panel, show data view
        let error_panel = error_panels.[container]
        LVGL.obj_del error_panel
        create_data_view container data
    
    // ... other transitions
```

This approach would combine Fabulous's diffing concept with compile-time analysis to generate minimal update code. The UI updates would stay functional in style while running as direct native calls.

## Integration with the MVU Pattern

The Model-View-Update pattern is central to Fabulous, and FidelityUI would preserve this architecture while adapting it for native execution. This is where FidelityUI would most strongly diverge from SwiftUI's approach. While SwiftUI uses property wrappers like `@State` and `@ObservedObject` to manage state, FidelityUI would stay true to the pure functional approach of MVU/Elmish.

Here's how a simple counter application would work:

```fsharp
// The developer writes standard MVU code
type Model = { Count: int }

type Msg = 
    | Increment
    | Decrement

let init() = { Count = 0 }

let update msg model =
    match msg with
    | Increment -> { Count = model.Count + 1 }
    | Decrement -> { Count = model.Count - 1 }

let view model dispatch =
    VStack() {
        Label($"Count: {model.Count}")
        Button("+", fun () -> dispatch Increment)
        Button("-", fun () -> dispatch Decrement)
    }
```

Composer would transform this into a static state machine with pre-allocated structures:

```fsharp
// Generated code structure
[<Struct>]
type AppState =
    { mutable Model: Model
      mutable View: lv_obj_t }

let mutable app_state = { Model = init(); View = null }

let dispatch msg =
    app_state.Model <- update msg app_state.Model
    update_view app_state.View app_state.Model

let init_app parent =
    app_state.View <- create_view parent app_state.Model dispatch
```

This transformation would remove the need for heap-allocated message queues while preserving the separation of concerns that MVU keeps. Where SwiftUI hides its state management behind property wrappers, our FidelityUI would keep the MVU pattern explicit, as Elmish developers expect.

## Learning from SwiftUI's Compilation Strategy

While we would maintain Clef's functional programming model, we can learn from SwiftUI's compilation strategy. SwiftUI's use of result builders (formerly function builders) to create DSLs is conceptually similar to Clef's computation expressions. However, where SwiftUI generates hidden types and protocols, FidelityUI's approach would be more transparent.

SwiftUI compiles its declarative views into efficient update graphs, eliminating unnecessary work. FidelityUI would take this concept further by doing even more work at compile time. Where SwiftUI might create a dependency graph at runtime, FidelityUI would pre-compute these relationships during compilation, generating specialized update functions for each possible state transition.

## Looking Forward: The Path to Production

As we envision our FidelityUI built on these foundations, the aim is to show that functional programming patterns can hold up in environments usually built with imperative, manual memory management. By drawing on the design work in Fabulous while adapting it for native compilation, we could carry the functional surface and the native target together.

The early stages of FidelityUI would test two questions. Can we keep Fabulous's API while compiling to native code with deterministic memory management? Can we preserve the functional programming experience while generating direct LVGL calls? The conceptual examples above suggest the design holds together, and the early work would set out to confirm it.

## Where the Pieces Come From

Our FidelityUI would draw from several sources:

- From **Fabulous**, we would take the widget model, the MVU pattern, and the overall architectural approach to functional UI
- From **SwiftUI**, we would learn about declarative UI syntax and compile-time optimization strategies
- From **LVGL**, we would take a mature rendering engine suited to embedded systems
- From **Clef and Elmish**, we would maintain the pure functional programming model that makes applications predictable and testable

But unlike any of these inspirations, FidelityUI would compile to native code with deterministic memory management, making it suitable for hard real-time systems while maintaining an idiomatic Clef developer experience.

As our framework matures, we would expand beyond basic layouts to animations, custom rendering with Skia, and richer interaction patterns. The foundation would stay the same: adapting functional patterns to native compilation, so Clef developers could target a wide range of platforms from one source.

For Clef developers, this would mean writing UI code in familiar patterns that compiles to native code, from embedded devices through to desktop applications. For the broader programming community, it would show functional programming and systems programming working from one source, with the compiler infrastructure carrying the high-level surface down to native calls.

Moving from Fabulous's managed, runtime-based approach to our envisioned compile-time, native implementation would carry those patterns into a context they were not originally built for: embedded and real-time systems with deterministic memory. We would be translating the prior work rather than setting it aside.

This is early design. As we keep working on FidelityUI, drawing on lessons from WPF and Fabulous, reading across from SwiftUI, and building on our Composer compilation pipeline, we are working toward a setup where Clef developers could target platforms from small embedded devices through server clusters while keeping the language features and patterns they already work in. That is the direction we will keep building toward as the rest of the ecosystem comes into place.
