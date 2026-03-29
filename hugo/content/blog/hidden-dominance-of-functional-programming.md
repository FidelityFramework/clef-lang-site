---
title: "The Hidden Dominance of Functional Programming"
date: 2022-02-02T16:59:54+06:00
description : "FP Became Successful by Hiding in Plain Sight"
tags: ["Analysis"]
authors: ["Houston Haynes"]
params:
  originally_published: 2022-02-02
  original_url: https://speakez.tech/blog/hidden-dominance-of-functional-programming/
  migration_date: 2026-03-29
---

> This article was originally published on the
> [SpeakEZ Technologies blog](https://speakez.tech) as part of our early
> design work on the Fidelity Framework. It has been updated to reflect
> the Clef language naming and current project structure.



When Simon Peyton Jones, one of the creators of Haskell and a renowned researcher in functional programming, explains Excel, he frames it in a fascinating way: "the easiest way to get at functional programming is to think of spreadsheets." This insight reveals a profound truth hiding in plain sight. While the programming world often debates the merits of functional programming versus object-oriented or imperative approaches, functional programming has quietly infiltrated mainstream computing in ways most users never realize.

## The Stealth Revolution

The triumph of functional programming hasn't come through academic persuasion or by converting traditional programmers to languages like Haskell or OCaml. Instead, it has succeeded by becoming embedded in tools used daily by hundreds of millions of people who would never identify themselves as programmers at all. This stealth revolution represents perhaps the greatest success story in the history of programming paradigms, a paradigm that conquered the world without most of its users even knowing its name.

## Excel: The World's Most Successful Functional Programming Platform

Microsoft Excel stands as the quintessential example of functional programming's hidden dominance. As Simon Peyton Jones noted in a 2021 Microsoft Research podcast, Excel fundamentally has functional language where formulas define the value of cells in terms of other cells, similar to how functions in Haskell or other functional languages define values in terms of other values.

"In a spreadsheet cell, you say, 'Here is a formula that gives the value of a cell.' And you compute the value of a whole spreadsheet full of cells by computing each cell, perhaps one at a time, perhaps in parallel, but in data dependency order," explains Peyton Jones when describing the functional nature of spreadsheets.

The market impact of Excel as a functional programming environment is staggering. In a presentation titled "Spreadsheets: Functional Programming for the Masses," Peyton Jones highlighted that while there are approximately 2 million "classic" programmers who write code in languages like C++ and C#, there are an estimated 50 million end-user programmers who use Excel formulas to build models. These users aren't professional programmers but rather engineers, teachers, financial analysts, and others who use Excel's functional paradigm without even realizing it.

Recent innovations in Excel have enhanced its capabilities as a functional language. The introduction of LAMBDA functions in 2021 made Excel's formula language Turing-complete, allowing users to define custom functions directly in the spreadsheet without resorting to VBA or other programming methods. As Brian Jones, head of product for Excel, notes: "there are orders of magnitudes more people that know how to write formulas, then know how to write JavaScript."

## Erlang: Functional Programming Powering Telecommunications

While Excel represents functional programming's quiet dominance in business applications, Erlang demonstrates how functional programming has become essential infrastructure for global communications.

Developed at Ericsson in the late 1980s, Erlang was created specifically to handle the demanding requirements of telecom systems. According to Erlang's official website, it was designed for "telecommunications applications, e.g. controlling a switch or converting protocols" and other systems requiring "distributed, reliable, soft real-time concurrent systems."

The impact of Erlang on our daily lives is far greater than most people realize. At the Code BEAM Stockholm conference in 2018, Cisco revealed that it ships 2 million devices per year running Erlang, translating to approximately 90% of all internet traffic passing through routers and switches controlled by Erlang. Additionally, Ericsson uses Erlang at the core of its GPRS, 3G, 4G, and 5G infrastructure, with a 40% market share of mobile telecommunications equipment.

What makes this particularly remarkable is that Erlang achieved this dominance not despite being a functional language, but because of it. The functional paradigm, combined with Erlang's actor model for concurrency, proved ideal for building fault-tolerant, scalable systems that must operate continuously without interruption.

## Beyond Excel and Erlang: The Expanding Functional Ecosystem

The functional programming paradigm has expanded well beyond these two prominent examples:

### WhatsApp and Chat Applications

WhatsApp, used by billions of people worldwide, was built using Erlang for its backend. The functional, concurrent nature of Erlang allowed WhatsApp to support millions of simultaneous connections with a remarkably small engineering team. Similarly, WeChat, China's dominant messaging platform with over a billion users, relies on Erlang for handling massive concurrency.

### Message Queues and Data Systems

RabbitMQ, a widely used message broker, is implemented in Erlang. CouchDB and Riak, popular NoSQL databases, also leverage Erlang's functional architecture to provide distributed, fault-tolerant data storage. These systems form critical infrastructure for countless applications across all industries.

### Elixir: Functional Programming for Web Development

Built on the Erlang virtual machine, Elixir has brought functional programming principles to web development through the Phoenix framework. As the Elixir website explains, the language and its ecosystem allow "developers to be productive in several domains, such as web development, embedded software, machine learning, data pipelines, and multimedia processing, across a wide range of industries."

### Functional Features in Mainstream Languages

Even traditionally imperative languages have increasingly adopted functional programming features:

- JavaScript has embraced functional patterns with arrow functions, map/reduce operations, and immutability libraries
- Python has incorporated functional programming constructs like list comprehensions and higher-order functions
- Java added lambda expressions and streams in Java 8
- C# has continuously expanded its functional programming capabilities by siphoning off examples from F#

## The Quiet Revolution: Why Functional Programming Succeeded Silently

The stealth success of functional programming offers fascinating insights into how paradigm shifts actually occur in technology:

### 1. Application Before Theory

Unlike object-oriented programming, which was heavily marketed as a paradigm, functional programming gained adoption through practical applications that solved real problems. Users of Excel don't care about lambda calculus or referential transparency; they care about building useful tools for business decision-making. Surfacing aspects of theory followed the practical success, rather than preceding it.

### 2. Domain-Specific Embedding

Functional programming found success by embedding itself within domain-specific tools rather than asking developers to switch languages entirely. Excel users don't think of themselves as functional programmers; they think of themselves as using Excel. This domain-specific embedding allowed functional concepts to spread without the resistance that comes with language switching.

### 3. Focus on End-User Benefits

The success of functional programming in tools like Excel comes from focusing on end-user benefits rather than programming philosophy. Spreadsheets make the functional paradigm's benefits of data dependency tracking and automatic recalculation immediately tangible to users without requiring them to understand the underlying concepts.

## The Future: From Hidden to Explicit

As functional programming continues to evolve, we're seeing a trend toward making its presence more explicit while maintaining its accessibility:

- Microsoft's addition of LAMBDA to Excel acknowledges and enhances its nature as a functional programming environment
- The popularity of Elixir demonstrates demand for accessible functional programming in web development
- F# aimed to bring functional-first programming to enterprise environments. Later it extended to the web via Fable. And now [the Clef language](https://clef-lang.com) embraces native applications and systems programming with the newly minted Fidelity framework.
- And even educational initiatives increasingly recognize Excel as a pathway to teaching programming concepts

## The Most Successful Programming Paradigm You Never Noticed

The story of functional programming's quiet dominance offers an important lesson about technology adoption. While computer science academia and professional software developers debate programming paradigms, the most successful paradigm may be the one that slipped into everyday use without most of its users ever realizing they were programming at all.

Excel has brought functional programming to hundreds of millions of people who would never write a line of code in Haskell. Erlang ensures our phone calls connect and our network traffic reach their destinations. Elixir powers websites we use daily. Functional programming hasn't just arrived, it has been here all along, hiding in plain sight, quietly reshaping how we interact with technology.

As Simon Peyton Jones might say, functional programming wasn't just an academic curiosity or a niche approach, it was the granite underlying the limestone of everyday computing, revealed as other paradigms gradually wore away.

## References

1. "Advancing Excel as a programming language with Andy Gordon and Simon Peyton Jones." Microsoft Research Podcast, July 19, 2021. https://www.microsoft.com/en-us/research/podcast/advancing-excel-as-a-programming-language-with-andy-gordon-and-simon-peyton-jones/

2. "Functional Programming Languages and the Pursuit of Laziness with Dr. Simon Peyton Jones." Microsoft Research Podcast, May 23, 2018. https://www.microsoft.com/en-us/research/podcast/functional-programming-languages-pursuit-laziness-dr-simon-peyton-jones/

3. "Spreadsheets: Functional Programming for the Masses." SlideShare, May 10, 2014. https://www.slideshare.net/kfrdbs/peyton-jones

4. "Excel: The Functional Programming Tool You Didn't Know You Had." The New Stack, February 8, 2022. https://thenewstack.io/excel-the-functional-programming-tool-you-didnt-know-you-had/

5. "Erlang (programming language)." Wikipedia. https://en.wikipedia.org/wiki/Erlang_(programming_language)

6. "Uses of Erlang in Telecom." Stack Overflow. https://stackoverflow.com/questions/2474129/uses-of-erlang-in-telecom

7. "What is Erlang." Erlang.org. https://www.erlang.org/faq/introduction.html

8. "Using Erlang for an Open Telecommunications Platform." Methods & Tools. https://www.methodsandtools.com/archive/erlang.php

9. "The Elixir programming language." Elixir-lang.org. https://elixir-lang.org/

10. "Beyond functional programming with Elixir and Erlang." Dashbit Blog. https://dashbit.co/blog/beyond-functional-programming-with-elixir-and-erlang
