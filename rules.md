### Diapositiva 1: Métodos para los tipos de datos STRING

* **REPASO**

**Declaración y asignación (definir):**  
 string texto \= "Hola Mundo";

*   
* **NOVEDAD\!\!**

Algunos tipos de datos tienen métodos y propiedades que nos pueden ser muy útiles\!  
 int largo \= texto.Length;  
string porcionTexto \= Texto.SubString(0,5);  
int posicionSeparacionPalabra \= Texto.IndexOf(" ");  
string nuevoTexto \= texto.Replace("Hola", "Chau");  
string textoMayuscula \= texto.ToUpper();  
string textoMinuscula \= texto.ToLower();  
char letra \= texto; // Un texto es una cadena de chars\!\!\!

* 

---

### Diapositiva 2: Tipo de datos DateTime

* **Declaración:**

*DateTime es un tipo de dato que nos sirve para representar fechas y horas. Se maneja como un objeto.*  
 DateTime miCumple;

*   
* **Asignación:**

*Como podemos asignar una fecha a un OBJETO de TIPO DateTime*  
 miCumple \= new DateTime(2009,02,25);  
DateTime fechaHoraNacimiento \= new DateTime(2009,02,25,9,20,0); // DD-MM-YYYY HH:MM:SS  
miCumple \= new DateTime(); // Todavía no se la fecha (constructor vacío)  
DateTime Hoy \= DateTime.Today; // Me devuelve el día en que se está ejecutando el programa  
DateTime HoyyAhora \= DateTime.Now; // Día y hora en que se está ejecutando el programa

* 

---

### Diapositiva 3: Colecciones

* **Definición:**  
  * *Una colección de datos es una estructura que agrupa múltiples elementos de un mismo tipo, permitiendo su manipulación de manera organizada y eficiente.*  
* **¿Qué tipos de datos vimos hasta ahora?**  
  * **LISTAS:** Que pueden tener un tamaño ilimitado de elementos.  
  * **ARRAYS:** En la definición indicamos cuántos elementos va a tener.  
* **Preguntas de repaso:**  
  * *¿Cómo se definían?*  
  * *¿Cómo se llenaban?*  
  * *¿Cómo se vacía una colección?*  
  * *¿Cómo se elimina un elemento?*  
  * *¿Cómo se recorrían?*

---

### Diapositiva 4: Colecciones \- Dictionary (Definición Teórica)

* **DEFINICIÓN TEÓRICA:**  
  * *Un diccionario es una colección de datos que almacena elementos en forma de pares clave-valor, donde cada clave es única y se asocia a un valor específico. Los diccionarios permiten acceder, modificar, agregar o eliminar elementos de manera eficiente.*  
* **CONSIDERACIONES IMPORTANTES:**  
  * **Claves:** Se le define el tipo de dato. Cuando creamos elementos en el diccionario las claves de un elemento no pueden repetirse dentro de la colección.  
  * **Valor:** También se le define un tipo de dato y al crear un elemento generamos una clave lo asociamos a dicho valor.  
  * **Búsqueda:** Utilizaremos la clave para buscar un valor deseado.  
  * **Recorrido:** Podemos recorrer el diccionario desde las claves o desde los valores para obtener todos los elementos.

---

### Diapositiva 5: Colecciones \- Dictionary (Definición Técnica)

**DEFINICIÓN TÉCNICA:**  
 Dictionary\<int, Alumno\> dicAlumnos \= new Dictionary\<int, Alumno\>();

*   
  * *En este caso estamos creando un diccionario de alumnos donde cada elemento tendrá una clave numérica y un valor del tipo Alumno.*  
  * *Este ejemplo nos puede servir para guardar el DNI como clave del alumno y toda la info de un alumno como Valor.*  
* **PEQUEÑAS DIFERENCIAS CON LISTAS EN SU UTILIZACIÓN:**  
  * **Agregar Elemento:** `dicAlumnos.Add(dni,objAlumno);`  
  * **Obtener un valor x su clave:** `dicAlumnos[dni]`  
  * **Búsqueda:** `dicAlumnos.ContainsKey(dni)`  
  * **Remover:** `dicAlumnos.Remove(dni)`

---

### Diapositiva 6: Colecciones \- Dictionary (Recorridos)

**RECORRER DICCIONARIO desde sus claves:**  
 foreach (int clave in dicAlumnos.Keys)  
{  
    Console.WriteLine(clave); // imprime el dni  
    Console.WriteLine(dicAlumnos\[clave\]); // imprime su valor  
}

* 

**RECORRER DICCIONARIO desde sus valores:**  
 foreach (Alumno objAlum in dicAlumnos.Values)  
{  
    Console.WriteLine(objAlum.Nombre); // imprime el nombre  
    // NO TENGO ACCESO CON ESTE RECORRIDO A LA CLAVE  
}

* 

---

### Diapositiva 7: Colecciones \- Dictionary (Iteración Completa)

**También es posible iterar teniendo en cuenta todo el par \[clave, valor\]:**  
 foreach (KeyValuePair\<int, Alumno\> alumno in dicAlumnos)  
{  
    Console.WriteLine(alumno.Key); // imprime el dni  
    Console.WriteLine(alumno.Value.Nombre); // imprime el nombre del alumno  
}

* 

**Mismo approach usando `var` (infiere el tipo KeyValuePair en la ejecución):**  
 foreach (var alumno in dicAlumnos)  
{  
    Console.WriteLine(alumno.Key); // imprime el dni  
    Console.WriteLine(alumno.Value.Nombre); // imprime el nombre del alumno  
}

### 1\. Fundamentos de .NET y Arquitectura MVC

* **.NET** es un framework de desarrollo de código abierto, gratuito y administrado, compatible con Windows, Linux y macOS. Un **framework** actúa como un esquema o marco de trabajo que provee un esqueleto inicial para evitar que el desarrollador deba plantear una estructura global desde cero, facilitando el trabajo colaborativo y la integración de librerías.  
* **MVC (Modelo-Vista-Controlador)** es un patrón de arquitectura sumamente extendido en aplicaciones web que separa de forma elegante la lógica de negocio y de acceso a datos de la interfaz de usuario. Este patrón se divide en tres componentes clave:  
  * **Modelo**: Representa los objetos de negocio, manipula los datos y gestiona los accesos y privilegios sobre dicha información.  
  * **Controlador**: Actúa como conductor u orquestador de la aplicación; captura las peticiones URL del usuario, invoca las operaciones del modelo y selecciona la vista apropiada como respuesta.  
  * **Vista**: Es la encargada de presentar la información procesada por el modelo en un formato visual adecuado para la interacción del usuario (usualmente marcado HTML/`.cshtml`).  
* Este patrón de arquitectura de backend no es exclusivo de .NET; se utiliza en muchas tecnologías y lenguajes como Python (Django), JavaScript (Express) o PHP (Laravel o Symfony).

---

### 2\. Preparación del Entorno y Estructura del Proyecto

* **Instalación**: Se requiere descargar e instalar el **.NET SDK** (`dotnet`) desde el sitio oficial de descargas de Microsoft.

**Creación del Proyecto**: En la terminal integrada de Visual Studio Code, se ejecutan los comandos para inicializar y abrir la aplicación:  
 dotnet new mvc \-n PrimerProyecto

code \-r PrimerProyecto

*   
* **Estructura del Proyecto**: Un proyecto MVC estándar cuenta con las siguientes carpetas y archivos esenciales:  
  * `/Controllers`: Aloja las clases controladoras de peticiones de URL.  
  * `/Models`: Contiene las clases de datos y objetos de negocio.  
  * `/Views`: Contiene los archivos de la interfaz de usuario con extensión `.cshtml`.  
  * `/wwwroot`: Almacena recursos estáticos como hojas de estilo (`.css`), scripts (`.js`), imágenes y librerías.  
  * `Program.cs` y archivos de configuración `appsettings.json`.  
* **Ejecución**: Antes de iniciar la aplicación por primera vez, se debe habilitar el certificado HTTPS de desarrollo escribiendo `dotnet dev-certs https --trust` en la terminal. Posteriormente, se presiona **F5** (o *Start Debugging*) para ejecutar el proyecto e ingresar al navegador mediante el puerto local asignado (por ejemplo, `https://localhost:5001/`).

---

### 3\. Implementación de Modelos, Controladores y Acciones

**Creación del Controlador**: Los controladores heredan de la clase base `Controller`. En un escenario típico de administración académica, el `HomeController.cs` puede definir métodos denominados **Action Methods** encargados de procesar la petición y devolver una respuesta mediante `return View();`:  
 using Microsoft.AspNetCore.Mvc;

namespace TP4.Controllers;

public class HomeController : Controller

{

    public IActionResult Index() \=\> View();

    public IActionResult ListaEstudiantes() \=\> View();

    public IActionResult InfoEstudiante() \=\> View();

}

*  Navegar a la ruta `[url]/Home/ListaEstudiantes` invocará de forma automática y transparente la acción correspondiente del controlador.

**Creación de un Modelo**: Se realiza dentro de la carpeta `/Models` creando un archivo de clase (por ejemplo, `Auto.cs`):  
 namespace PrimerProyecto.Models

{

    public class Auto

    {

        // Definición de propiedades y constructor...

    }

}

* 

**Instanciación e Intercambio de Datos**: En el Action Method del controlador se instancia el modelo y se puede transferir hacia la vista de manera dinámica a través de **ViewBag** o **ViewData**:  
 private static Auto miAuto;

public IActionResult Index()

{

    miAuto \= new Auto("AAA123", "Chevrolet");

    ViewBag.MiAuto \= miAuto; // Se asigna al ViewBag para enviarlo a la vista

    return View();

}

* 

---

### 4\. Vistas, Razor y Enlaces Dinámicos

* **Razor**: Es un motor de vistas que utiliza la sintaxis `@{ .. }` para permitir la ejecución de código C\# directamente incrustado dentro de los archivos HTML (`.cshtml`).  
* **Envío de Datos Dinámicos**:  
  * **ViewData**: Estructura de diccionario de clave-valor útil para pasar datos sencillos (ej: `ViewData["Title"] = "Index";`).

**ViewBag**: Objeto dinámico de tipo *wrapper* donde se pueden definir variables sobre la marcha sin clases formales:  
 \<\!-- En la vista se consumen las variables de ViewBag \--\>

\<h1\>El Nombre del Alumno es @ViewBag.NombreAlumno y tiene @ViewBag.Edad años\</h1\>

* 

**Generación de Enlaces (URL.Action)**: Se utiliza el helper `@Url.Action` en el atributo `href` de las etiquetas de enlace (`<a>`) para determinar dinámicamente la ruta a la que apunta la acción de un controlador. Admite el paso de objetos anónimos como parámetros de consulta:  
 \<\!-- Sintaxis básica \--\>

\<a href='@Url.Action("InfoEstudiante", "Estudiante")'\>Federico Perchuk\</a\>

\<\!-- Envío de parámetros al Action Method \--\>

\<a href='@Url.Action("ListadoEstudiantes", "Estudiante", new { Curso="4A" })'\>Ir al Curso 4A\</a\>

*   
* **Diferencia clave en navegación**:  
  * `Return View`: Renderiza y devuelve la vista especificada directamente en el servidor sin generar una nueva solicitud HTTP en el navegador.  
  * `RedirectToAction`: Redirige al navegador a una acción diferente, provocando una nueva solicitud HTTP completa.

---

### 5\. Páginas Maestras (MasterPages) y Enlaces Oficiales

* **MasterPages (`_Layout.cshtml`)**: Al crear un proyecto, bajo la carpeta `/Views/Shared` se define el archivo `_Layout.cshtml`. Este archivo almacena las estructuras HTML repetitivas (menús, pies de página, metas de cabecera) para evitar duplicar código en cada vista individual.  
* **Función `@RenderBody()`**: Es la instrucción Razor más importante dentro de una MasterPage. Al interpretarse el sitio web, .NET Core renderiza la estructura general del Layout y "pega" de forma dinámica el contenido de la vista solicitada justamente en la posición que ocupa `@RenderBody()`.  
* **Enlaces y documentación oficial**: La presentación incluye hipervínculos clave para la consulta de información de referencia de Microsoft:  
  * [Tutorial de Introducción a ASP.NET Core MVC](https://learn.microsoft.com/aspnet/core/tutorials/first-mvc-app/)  
  * [Guía completa de ASP.NET Core MVC](https://learn.microsoft.com/aspnet/core/mvc/overview)  
  * [Crear una API en .NET Core](https://learn.microsoft.com/aspnet/core/tutorials/first-web-api)

### Diapositiva 1: Introducción a los Formularios

**Cualquier página HTML puede mejorarse con formularios interactivos** que pidan al visitante que rellene información: introducir texto, seleccionar opciones, confirmar con un botón...

* Su **propósito** es recopilar datos del usuario y enviarlos a un servidor. Es fundamental para la interacción y la recolección de datos en aplicaciones web.  
* Como vamos a ver, **los resultados tienen que procesarse en otro lenguaje**, como **ASP.NET**, por ejemplo. Mientras tanto, tenemos un gran número de etiquetas HTML por descubrir.

**Bienvenidos y bienvenidas, al maravilloso mundo de los formularios.**

---

### Diapositiva 2: El Tag FORM

Cuando tienes la necesidad de **insertar un formulario en tu página HTML**, necesitas empezar escribiendo una etiqueta `<form>`. **Este tag debe tener su tag de cierre `</form>`**.

* Dentro de este tag escribiremos muchos **tags de ingreso de datos** que conoceremos en detalle en esta presentación.  
* Se escriben en las **Views** ya que es el ingreso de datos que se le presenta al usuario.

---

### Diapositiva 3: Atributos Esenciales del TAG Form

Existen muchos atributos más, pero vamos a hacer hincapié en estos **tres atributos esenciales**:

* **`action`**: Define la URL para enviar los datos que el usuario ingresa en un form. La seguridad (HTTPS) es clave.  
* **`method`**: Este atributo especifica **cómo** se van a enviar los datos que el usuario escriba en el formulario.  
* **`enctype`**: Define el tipo de codificación que se usará al enviar los datos del formulario al servidor mediante el método POST.

---

### Diapositiva 4: Valores Posibles en Atributos Esenciales

#### 1\. Atributo `action`

Especifica a qué dirección web se enviarán y procesarán los datos. En el caso de un **proyecto MVC la dirección SIEMPRE debe ser una acción del controller**.

#### 2\. Atributo `method`

* **`GET`**: La información se envía en la URL de la página y es visible. Solo permite 255 caracteres.  
* **`POST`**: Es el método más usado en formularios, ya que permite mayor cantidad de información y es más seguro porque los datos no se muestran en la barra de direcciones.

#### 3\. Atributo `enctype`

* **`application/x-www-form-urlencoded`**: Los datos del formulario se codifican como pares `clave=valor`, separados por `&` (es el enctype por default de envío).  
* **`multipart/form-data`**: Se utiliza cuando el formulario incluye archivos. Esta codificación permite enviar tanto archivos como otros datos.

**Ejemplo de sintaxis del Form:**

\<form action='@Url.Action("GuardarDatos", "Home")' method="POST" enctype="multipart/form-data"\>

---

### Diapositiva 5: TAGS para Campos de Entrada Básicos (Campos de Texto)

HTML provee varios tags para **introducir texto** en un formulario:

**Texto de una línea**:  
 \<input type="text" name="nombre" /\>

* 

**Tipo clave (Password)**:  
 \<input type="password" name="contraseña" /\>

* 

**En múltiples líneas**:  
 \<textarea name="descripcionLarga" rows=10 cols=5 /\>

* 

**Textos validados**:  
 \<input type="email" name="mail" /\>

\<input type="tel" name="telefono" /\>

\<input type="color" name="colorPelo" /\>

\<input type="url" name="paginaWeb" /\>

\<input type="date" name="fecha" /\>

\<input type="number" name="edad" /\>

* 

*Nota: Todos estos tags tienen más atributos para investigar, como `placeholder`, `maxlength` y los que ya conocieron en Arte, como `Class`, `Id`, etc. ¡Para investigar\!*

---

### Diapositiva 6: TAGS con Opciones de Selección (Concepto)

HTML incluye un montón de **opciones que pueden usarse en el formulario**. Son elementos que requieren que el visitante elija a partir de una lista de posibilidades:

1. **Casillas de selección única** (Rojo, Azul, Verde).  
2. **Casillas de selección múltiple** (Deportes, Música, Cine).  
3. **Listas desplegables** (Argentina, España, México, Chile).

---

### Diapositiva 7: Código de Tags con Opciones de Selección

#### 1\. Casillas de selección única (Radio Buttons)

\<input type="radio" name="color" value="rojo"\> Rojo

\<input type="radio" name="color" value="azul"\> Azul

\<input type="radio" name="color" value="verde"\> Verde

* **Comportamiento**: En el input `radio` la información que se guardará es el `value` seleccionado.

#### 2\. Casillas de selección múltiple (Checkboxes)

\<input type="checkbox" name="deportes"\> Deportes

\<input type="checkbox" name="musica"\> Música

\<input type="checkbox" name="cine"\> Cine

* **Comportamiento**: En el input `checkbox` la información que se guardará será un `true` si está seleccionado o `false` en caso contrario.

#### 3\. Listas desplegables (Select)

\<select name="pais"\>

    \<option value="1"\>Argentina\</option\>

    \<option value="2"\>España\</option\>

    \<option value="3"\>México\</option\>

    \<option value="4"\>Chile\</option\>

\</select\>

* **Comportamiento**: En el tag `select` la información que se guardará también es el `value` del option seleccionado.

*Nota: Dentro de todos estos tags tenemos atributos muy interesantes como **`Checked`**, **`Required`** y **`Selected`** para investigar cómo funcionan*.

---

### Diapositiva 8: TAG Hidden y TAG File

#### TAG Hidden (Oculto)

El elemento input, cuyo atributo `Type` se define con el valor `"hidden"`, representa cualquier cadena de texto arbitraria que **no está pensada para ser vista o editada por el usuario**.

* Los controles ocultos son **especialmente útiles para enviar datos al servidor definidos por el autor**, basados o no en la interacción con el usuario.

\<input type="hidden" name="IdPelicula" value="1" /\>

#### TAG File (Carga de Archivos)

El elemento input, cuyo atributo `Type` se define con el valor `"file"`, nos sirve para **pedirle al usuario que suba un archivo**.

* Este puede ser una imagen, documento o cualquier archivo de nuestra computadora que queramos enviar al servidor.

\<input type="file" name="foto" /\>

---

### Diapositiva 9: Botones de Envío

Toda la información cargada por un usuario mediante un navegador se debe enviar para procesar. Para esto se utilizan los **botones de envío**:

**`submit`**: Botón de envío principal del formulario. Redirecciona a la página especificada en el atributo `action` del tag `form`.  
 \<input type="submit" value="Guardar" /\>

* 

**`reset`**: Restablece el formulario, eliminando todo lo cargado por el usuario.  
 \<input type="Reset" value="Borrar Formulario" /\>

* 

**`image`**: Equivalente al botón `submit`, pero visualmente será una imagen. Añade el atributo `src` para especificar la URL de la imagen.  
 \<input type="image" src="/img/guardar.jpg" /\>

* 

**`button`**: Botón genérico que no tendrá efecto alguno por sí solo. En general, este botón se maneja con JavaScript para desarrollar acciones dinámicas en la página.  
 \<input type="button" onclick="funcion()" /\>

* 

---

### Diapositiva 10: Recepción de Información de un FORM

Habiendo aprendido cómo crear un formulario web para interactuar con un usuario, ahora lo que debemos aprender es **qué hacer con los datos que son enviados** desde un tag `form`. En esta sección aprenderemos a recibir dichos datos para luego tomar una acción (guardarlos en un objeto, enviarlos a otra página, etc.).

---

### Diapositiva 11: Introducción a la Recepción de Datos

Para recibir los datos enviados desde un formulario como el de la diapositiva 4:

\<form action='@Url.Action("GuardarDatos", "Home")' method="POST" enctype="multipart/form-data"\>

Debemos crear una acción en el controller **"Home"** llamada **"GuardarDatos"**. Ahí recibiremos toda la información del form en forma de **Parámetros**.

⚠️ **¡Atención\!** Los nombres de los parámetros en el controlador **deben llamarse igual** a los nombres de los controles creados en la View (es decir, corresponder con el atributo `name`).

---

### Diapositiva 12: Ejemplo de Recepción de Información

#### En la View:

\<form action="@Url.Action("Login", "Cuenta")" method="post"\>

    Usuario: \<input type="text" name="usuario"\>

    Contraseña: \<input type="password" name="contrasena"\>

    \<input type="submit" value="Entrar"\>

\</form\>

* *Nota: Aquí se destaca que el atributo `name` es fundamental para enlazar con el backend.*

#### En el controller `Cuenta`:

public class CuentaController : Controller

{

    \[HttpPost\]

    public ActionResult Login(string usuario, string contrasena)

    {

        // Aquí podrías validar usuario y contraseña si quisieras.

        ViewBag.Usuario \= usuario;

        return View("Bienvenido");

    }

}

---

### Diapositiva 13: Recepción de Archivos de un tag Form (View)

Si estamos enviando un archivo desde un formulario, debemos **recordar siempre poner el `enctype`** en el tag form.

#### En la View:

\<form action='@Url.Action("SubirArchivo","Home")' method="post" enctype="multipart/form-data"\>

    \<input type="file" name="archivo" /\>

    \<br /\>\<br /\>

    \<button type="submit"\>Subir\</button\>

\</form\>

* Cuando el usuario presione el botón de Submit, se ejecutará la acción **"SubirArchivo"** que está en el action del formulario.  
* La información del formulario se enviará por **POST**.  
* El enctype se usará **`multipart/form-data`** porque este formato divide el contenido en partes y permite enviar tanto texto como binarios (archivos) en la misma petición sin corromperlos.

---

### Diapositiva 14: Recepción de Archivos en el Controller

`IWebHostEnvironment` es un servicio proporcionado por el framework que nos dice: la ruta física del proyecto, la carpeta raíz del contenido estático (`wwwroot`) y el nombre del entorno (Desarrollo, Producción, etc.).

#### En el controller `Home`:

private readonly IWebHostEnvironment \_env;

public HomeController(IWebHostEnvironment env)

{

    \_env \= env;

}

public IActionResult SubirArchivo(IFormFile archivo)

{

    if (archivo \!= null && archivo.Length \> 0\)

    {

        // Nombre del archivo

        string nombreArchivo \= archivo.FileName;

        // Ruta física donde guardar

        string rutaCarpeta \= Path.Combine(\_env.WebRootPath, "imagenes");

        if (\!Directory.Exists(rutaCarpeta))

        {

            Directory.CreateDirectory(rutaCarpeta);

        }

        string rutaCompleta \= Path.Combine(rutaCarpeta, nombreArchivo);

        // Guardar el archivo en wwwroot/imagenes

        using (var stream \= new FileStream(rutaCompleta, FileMode.Create))

        {

            archivo.CopyTo(stream);

        }

        return View("Resultado");

    }

    // Si no se seleccionó ningún archivo no ejecuta el return view de Resultado

    ViewBag.Mensaje \= "No se seleccionó ningún archivo.";

    return View("Index");

}

---

### Diapositiva 15: Paso de Parámetros Avanzados

Se usa con `RedirectToAction` para pasar datos simples (`int`, `string`, etc.) entre acciones (`ActionResult`). Los datos se agregan a la URL como parte de la ruta o query string.

\[HttpPost\]

public IActionResult RegistrarAlumno(int id, string nombre)

{

    // Redirige pasando los datos por la URL

    return RedirectToAction("MostrarAlumno", new { id \= id, nombre \= nombre });

}

public IActionResult MostrarAlumno(int id, string nombre)

{

    // Pasa los datos a la vista usando ViewBag

    ViewBag.Id \= id;

    ViewBag.Nombre \= nombre;

    return View();

}

### Diapositiva 1: Recordando lo aprendido el año pasado:

**En 3ero creabamos métodos para manejar los atributos de las clases, por ejemplo:**

class Persona

{

    private string nombre; // Atributo privado

    // Método para devolver el nombre

    public string DevolverNombre()

    {

        return nombre;

    }

    // Método para cambiar el nombre

    public void CambiarNombre(string nuevoNombre)

    {

        nombre \= nuevoNombre;

    }

}

---

### Diapositiva 2: Controlando el acceso a atributos

**Este año, algunos atributos vamos a necesitar accederlos desde otras clases de forma pública.**

#### ¿Pero cómo controlar lo que se puede hacer con esos atributos públicos?

* **¿Deberían otras clases solo leer el atributo?**  
* **¿Deberían poder modificarlo también?**

#### ¿Cómo podemos manejarlo?

La solución está en **getters** y **setters**, que nos permiten definir si queremos que un atributo público sea **solo lectura, solo escritura, o lectura y escritura**.

---

### Diapositiva 3: ¿Qué son Getters y Setters?

Los **getters** y **setters** son métodos especiales que nos permiten leer y modificar los valores de los atributos de una clase, pero de una forma controlada.

* **Getter**: Nos permite **leer** el valor de un atributo.  
* **Setter**: Nos permite **modificar** el valor de un atributo.

**En C\#, podemos usar propiedades automáticas para crear getters y setters sin necesidad de escribir métodos adicionales como veníamos haciendo. Esto simplifica el código.**

---

### Diapositiva 4: Sintaxis de Propiedades Automáticas

**Podemos usar propiedades automáticas que hacen todo lo mencionado previamente de forma más sencilla**

class Persona

{

    public string Nombre { get; set; } // Propiedad automática de lectura y escritura

}

#### Uso:

class Program

{

    static void Main()

    {

        Persona p \= new Persona();

        p.Nombre \= "Carlos"; // Modificamos el valor

        Console.WriteLine(p.Nombre); // Leemos el valor

    }

}

> **Este es el caso más sencillo donde cualquiera puede leer y modificar el valor**.

---

### Diapositiva 5: Controlando la accesibilidad

**A veces no queremos que todo sea completamente accesible para ser modificado desde fuera de la clase. Podemos querer que la lectura sea pública, pero restringir la escritura, o al revés.**

class Persona

{

    public string Nombre { get; private set; } // Solo lectura pública, escritura privada

}

#### Uso:

class Program

{

    static void Main()

    {

        Persona p \= new Persona();

        p.Nombre \= "Carlos"; // ERROR: No se puede modificar desde afuera, porque el set es privado

        Console.WriteLine(p.Nombre); // Se puede leer el valor

    }

}

* **En este caso el get sigue siendo público por lo que cualquier clase puede leer el valor.**  
* **Con el set privado solo la clase Persona puede modificar el valor de Nombre**.

---

### Diapositiva 6: Enviando Parámetros del Form a un Objeto

**Si tenemos muchos parámetros que enviar a un controller y encima después ponerlos en un objeto ahora podemos directamente llenarlo en la recepción de parámetros de la acción del controller**

\<div class="text-center"\>

    \<h1 class="display-4"\>Bienvenido @ViewBag.Nombre @ViewBag.Contraseña\</h1\>

    \<form action='@Url.Action("Login","Home")' method="POST" \>

        \<input type="text" name="NombreUsuario" placeholder="Usuario"\>

        \<input type="password" name="Contraseña" placeholder="Password"\>

        \<input type="submit" value="Ingresar"\>

    \</form\>

\</div\>

#### Uso:

\[HttpPost\]

// 0 references

public IActionResult Login(Usuario user)

{

    ViewBag.Nombre \= user.NombreUsuario;

    ViewBag.Contraseña \= user.Contraseña;

    return View("Index");

}

* **¡Recordar que los NAME de cada input deben llamarse IGUAL a los atributos de la clase\!**  
* **En este caso además, todos los atributos deben tener disponible la propiedad set.**

---

### Diapositiva 7: Conclusión

* **Usando getters y setters automáticos podemos hacer que nuestros atributos sean accesibles de manera sencilla, sin escribir métodos adicionales.**  
* **A veces necesitamos controlar el acceso a los atributos, y para eso podemos usar private para evitar modificaciones o lecturas no deseadas desde otra clase.**

### Diapositiva 1: Índice de Contenidos

* **01** Introducción  
* **02** Sintaxis  
* **03** Estructuras  
* **04** Arrays  
* **05** DOM  
* **06** Enlaces complementarios *(Detalle visual: La portada muestra un hexágono azul con el logo amarillo cuadrado de JS en el centro).*

---

### Diapositiva 2: Introducción

> "Si el término **frontend** refiere a todo aquello que sucede del lado del cliente (quien consume los datos y no los genera/procesa) el máximo aliado y un jugador fundamental es *JavaScript*.

> Sin JavaScript, nuestros sitios son bastante estáticos, carecen de flujos diferentes dependiendo de nuestras acciones en la interfaz, entre otras cosas. Hemos llegado a una instancia en que es tan importante el negocio como las buenas prácticas de **UX/UI**.

> Pero... ¿qué es *JavaScript*?"

---

### Diapositiva 3: ¿Qué es JavaScript? \- Definición

> "Es un **lenguaje de programación** que implementa funciones complejas en páginas web. Interviene cada vez que una página web hace algo más que sentarse allí y mostrar información estática para que la veas; muestra oportunas actualizaciones de contenido, mapas interactivos, animación de Gráficos 2D/3D, desplazamiento de máquinas reproductoras de vídeo, etc.

> Técnicamente, *JavaScript* es un **lenguaje de programación interpretado**, por lo que no es necesario compilar los programas para ejecutarlos. En otras palabras, los programas escritos con JavaScript se pueden probar directamente en cualquier navegador sin necesidad de procesos intermedios."

---

### Diapositiva 4: ¿Qué es JavaScript? \- Las Capas Web

> "Es la **tercera capa del pastel** de las tecnologías web estándar, dos de las cuales (HTML y CSS) hemos cubierto en la materia de Arte."

*(Detalle visual: Se muestra la ilustración de un pastel de tres capas con una vela: la capa superior es **HTML** \[Estructura\], la del medio es **CSS** \[Estilo\], y la inferior es **JavaScript** \[Comportamiento\]).*

---

### Diapositiva 5: ¿Qué es JavaScript? \- Motores y Relación con Java

> "Cada navegador (Google Chrome, Safari, Edge...) tiene su propio **motor de Javascript**, cuyo objetivo es interpretar el lenguaje y mostrar en el explorador el resultado del mismo.

> A pesar de su nombre, *JavaScript* **no guarda ninguna relación directa** con el lenguaje de programación Java."

---

### Diapositiva 6: Incluyendo JavaScript en mi HTML

Muestra cómo enlazar un archivo externo de JavaScript al final del `<body>` de un documento HTML para optimizar la carga del sitio web:

\<\!DOCTYPE html\>

\<html lang="en"\>

\<head\>

    \<meta charset="UTF-8" /\>

    \<meta name="viewport" content="width=device-width, initial-scale=1.0" /\>

    \<title\>JavaScript\</title\>

\</head\>

\<body\>

    \<script src="js/script.js"\>\</script\>

\</body\>

\</html\>

---

### Diapositiva 7: Variables \- Let y Const

* "JavaScript es un lenguaje de **tipado dinámico**. Es decir, las variables toman su tipo de dato en tiempo de ejecución.  
* Vamos a utilizar **`let`** y **`const`** para declarar variables. La diferencia entre ambas instrucciones radica en que `const` no permite modificar el valor de la variable, y debe ser inicializada inmediatamente en la misma línea en la cual es declarada.  
* `let` permite la modificación futura del valor de la variable.  
* Ambas formas de declarar una variable tienen **scope de bloque**, es decir, viven entre las llaves `{ }` donde fueron declaradas."

---

### Diapositiva 8: Variables \- Ejemplo de Ámbito y Mutabilidad

Ejemplo práctico de declaración y modificación de variables:

function saludar(){

    const nombre \= "Ezequiel";

    console.log("Hola " \+ nombre \+ "\!");

    nombre \= "Juan"; // \-\> da error porque no se puede modificar una const

    console.log("Hola " \+ nombre \+ "\!");

    let apellido \= "Perez";

    console.log("Hola " \+ apellido \+ "\!");

    apellido \= "Gonzalez"; // \-\> ok porque se puede modificar un let

    console.log("Hola " \+ apellido \+ "\!");

}

---

### Diapositiva 9: Variables \- Tipos de Datos y Dinamismo

> "Como hemos mencionado anteriormente, JavaScript es un lenguaje **débilmente tipado y dinámico**. Las variables en JavaScript no están asociadas directamente con ningún tipo de valor en particular, y a cualquier variable se le puede asignar (y reasignar) valores de todos los tipos."

let foo \= 42; // foo ahora es un número

foo \= "bar";  // foo ahora es un string

foo \= true;   // foo ahora es un booleano

---

### Diapositiva 10: Variables \- Tipos de Datos Primitivos

"Los tipos de datos **primitivos** en JavaScript son:

* `undefined`  
* `null`  
* `boolean`  
* `number`  
* `string`  
* `symbol`

Existen también otros **3 tipos de datos especiales**: `Null`, `Object` y `Function`."

---

### Diapositiva 11: Asignación de variables

> "La asignación de variables permite otorgarle valores a variables ya declaradas, para luego utilizarlas donde corresponda."

// let permite no ser asignado al ser declarado

let nombre;

nombre \= "Lionel";

// const debe ser asignado al momento de su declaración

const apellido \= "Scaloni";

---

### Diapositiva 12: Condicionales

> "Los condicionales en JavaScript funcionan y se definen exactamente igual que en otros lenguajes de programación, como por ejemplo C\#."

**Uso de `if / else if / else`:**

if (num1 \> num2) {

    console.log('Num 1 es mayor a num 2');

} else if (num1 \=== num2) {

    console.log('Son iguales');

} else {

    console.log('Num 2 es mayor a num 1');

}

**Uso de `switch`:**

switch (categoria) {

    case 'A':

        console.log(precio \* TIPO\_A);

        break;

    case 'B':

        console.log(precio \* TIPO\_B);

        break;

    default:

        console.log('categoría inexistente');

        break;

}

---

### Diapositiva 13: Ciclos \- Introducción

> "La definición de los ciclos es muy similar a otros lenguajes de programación de alto nivel. Un cambio que vemos es el uso de **`let`** en el `for`:"

// Ciclo for

for(let i \= 0; i \< 100; i++) {

    console.log(i);

}

// Ciclo while

while(\!encontrado) {

    //código

}

// Ciclo do while

do {

    //código

} while(\!encontrado);

> "A medida que vayamos viendo objetos, funciones y arrays, exploraremos otras formas de uso del ciclo `for` y `foreach`."

---

### Diapositiva 14: Funciones \- Definición

> "Las funciones son bloques de código que pueden recibir o no parámetros, y retornan o no un resultado concreto.

> **Los parámetros en JavaScript no son tipados**"

function sumar(num1, num2) {

    return num1 \+ num2;

}

---

### Diapositiva 15: ¿Qué es el DOM?

> "El **modelo de objeto de documento (DOM)** es una interfaz de programación para los documentos HTML. El DOM da una representación del documento como un grupo de nodos y objetos estructurados que tienen propiedades y métodos.

> **Esencialmente, conecta las páginas web a scripts o lenguajes de programación.**" *(Resaltado en amarillo)*

---

### Diapositiva 16: Interfaz Document

> "La interfaz `Document` describe las propiedades y métodos comunes para cualquier tipo de documento. Document define algunos métodos interesantes que nos van a servir para acceder desde JavaScript a los elementos dispuestos en HTML."

* ✨ **`document.getElementById(id)`** *(Resaltado en amarillo en la diapositiva)*  
* `document.getElementsByTagName(name)`  
* `document.getElementsByClassName(name)`  
* `document.createElement(element)`  
* `document.removeChild(element)`  
* `document.appendChild(element)`

---

### Diapositiva 17: document.getElementById

> "Devuelve una referencia al elemento por su **ID**"

**Estructura HTML:**

\<p id="parrafo"\>\</p\>

**Código JavaScript:**

const parrafo \= document.getElementById('parrafo')

---

### Diapositiva 18: element.innerHTML

> "Cualquier elemento que forme parte del document de HTML, tiene accesible la propiedad **`innerHTML`**. Esta representa el contenido que tiene ese elemento dentro de la página web."

const parrafo \= document.getElementById('parrafo')

parrafo.innerHTML \= "Taller de Programación"

// Luego de esta línea, el párrafo tendrá "Taller de Programación" como contenido

---

### Diapositiva 19: Propiedades de elementos (input Text)

> "Cuando obtenemos un elemento mediante JS con el DOM, podemos acceder a todas sus propiedades. Nombraremos algunas pero investigar en la web cuando se necesiten acceder a otras."

**HTML:**

\<input type="text" name="NombreEmpleado" value="Juan" id="Nombre"\>

**JavaScript:**

let nombre \= document.getElementById("Nombre");

console.log(nombre.value); // Muestra "Juan" en consola

nombre.value \= "Leo";      // Cambia el nombre en el value a "Leo"

nombre.style.color \= "red"; // Cambia el color del texto del input a rojo

---

### Diapositiva 20: Propiedades en elementos (button)

> "Un ejemplo sobre cómo podemos aprovechar el **`onclick`** de un botón y además crearle una propiedad para utilizarla en JS."

**HTML:**

\<button id="Actualizar" onclick="cambiarColor()" estado="Azul"\>Cambiar Color\</button\>

\<h1 id="MostrarTexto"\>Leo\</h1\>

**JavaScript:**

\<script\>

    function cambiarColor()

    {

        let Boton \= document.getElementById("Actualizar");

        const objH1 \= document.getElementById("MostrarTexto");

        objH1.style.color \= (Boton.estado \== "Azul" ? 'green' : 'blue');

        Boton.estado \= (Boton.estado \== "Azul" ? 'Verde' : 'Azul');

    }

\</script\>

### Diapositiva 1: CONTENIDO

* **Título principal:** CONTENIDO  
* **Estructura del índice:**  
  * **01** Introducción  
  * **02** Instalación de Librerías  
  * **03** Acerca de Dapper ORM  
  * **04** Dapper \- Query  
  * **05** Dapper \- Execute  
  * **06** Enlaces complementarios  
* **Ilustración visual:** Un gráfico conceptual a la derecha que muestra servidores de bases de datos de color gris y azul conectados mediante líneas punteadas con un ordenador portátil que visualiza gráficos de barras y de tarta junto a carpetas de archivos, acompañado de un engranaje en la parte inferior.

---

### Diapositiva 2: Introducción

* **Título principal:** Introducción  
* **Texto de la diapositiva:**  
  * "Hasta ahora, los programas o sistemas que veníamos realizando, estaban basados 100% dentro de la memoria principal. La memoria principal de un dispositivo informático es volátil: es decir, una vez que termina el programa o finaliza la ejecución del servidor, los datos se pierden."  
  * "En esta unidad, veremos cómo persistir y recuperar la información que circula en nuestros proyectos, desde una base de datos creada en el motor **Microsoft SQL Server**."

---

### Diapositiva 3: Instalación de librerías

* **Título principal:** Instalación de librerías  
* **Texto de la diapositiva:**  
  * "En la terminal del VSCode vamos a correr de forma secuencial los siguientes comandos:"  
    * **`dotnet add package Microsoft.Data.SqlClient`**  
    * **`dotnet add package Dapper`**  
  * "Estas sentencias van a instalar los paquetes SqlClient y Dapper, necesarios para poder conectarnos a nuestra base de datos SQL Server."

---

### Diapositiva 4: Comprobamos ambas instalaciones

* **Título principal:** Comprobamos ambas instalaciones  
* **Texto de la diapositiva:**  
  * "Si ingresamos al archivo **.csproj** de nuestro proyecto, que se encuentra en la carpeta raíz, deberíamos ver las dos librerías instaladas correctamente."  
* **Código del archivo .csproj:**

1 \<Project Sdk="Microsoft.NET.Sdk.Web"\>

2

3   \<PropertyGroup\>

4     \<TargetFramework\>net7.0\</TargetFramework\>

5     \<Nullable\>enable\</Nullable\>

6     \<ImplicitUsings\>enable\</ImplicitUsings\>

7     \<RootNamespace\>TP\_Prog\</RootNamespace\>

8   \</PropertyGroup\>

9

10   \<ItemGroup\>

11     \<PackageReference Include="Dapper" Version="2.1.66" /\>

12     \<PackageReference Include="Microsoft.Data.SqlClient" Version="6.0.1" /\>

13   \</ItemGroup\>

14 \</Project\>

---

### Diapositiva 5: La librería SqlClient

* **Título principal:** La librería SqlClient  
* **Texto de la diapositiva:**  
  * "Vamos a seguir basándonos en el concepto que, en el lenguaje C\# (sobre .NET Core) todo está orientado a objetos. Por lo tanto, podemos incorporar librerías de clases ya desarrolladas que nos permitan solucionar diversos requerimientos. En este caso, el objetivo es conectarnos a un servidor SQL Server, donde tenemos alojada la base de datos que queremos acceder."  
  * "Agregamos una nueva directiva using:"  
    * **`using Microsoft.Data.SqlClient;`**  
  * "Una vez incorporada la librería a la clase, podremos utilizar las clases que proporciona, entre las cuales figura: SqlConnection. Esta clase permite crear objetos que contienen conexiones a los servers que alojan nuestras bases."

---

### Diapositiva 6: Incorporando SqlClient

* **Título principal:** Incorporando SqlClient  
* **Texto de la diapositiva:**  
  * "En nuestro sistema, tendremos disponible un único acceso a la base de datos, es por eso que utilizaremos una clase con métodos estáticos dentro de model para poder manejar toda la lógica de persistencia de datos."  
  * "Creamos la clase **BD.cs** e incorporamos la directiva para importar **SQLClient**."  
* **Esquema visual del explorador de archivos en VS Code:**  
  * `TRY-CATCH-POC`  
    * `bin`  
    * `Controllers`  
      * `HomeController.cs`  
    * `Models`  
      * `BD.cs`  
      * `Cliente.cs`  
* **Contenido inicial del archivo BD.cs:**

1 using Microsoft.Data.SqlClient;

2 using Dapper;

3

4 namespace try\_catch\_poc.Models;

5

6 public class BD

7 {

---

### Diapositiva 7: ORM

* **Título principal:** ORM  
* **Texto de la diapositiva:**  
  * "ORM (Mapeo Objeto-Relacional en español) es una técnica de programación que permite "mapear" las estructuras de una base de datos relacional al modelo orientado a objetos de una aplicación."  
  * "De esta forma ahorraremos tiempo (y mucho esfuerzo trivial) programando la asignación de valores a los atributos de los objetos."  
* **Diagrama visual explicativo (Mapeo Objeto-Relacional):**  
  * Muestra la equivalencia entre un modelo orientado a objetos en código y su contraparte en tablas de bases de datos relacionales, unidos por flechas de dirección bidireccionales cruzadas por una barrera de división amarilla.  
  * **Objeto Producto (A Product Object):**  
    * `id: 12`  
    * `name: Bike`  
    * `price: $800.00`  
    * `description: fixed gear, blue, fast`  
  * **Tabla de Productos (Table: product):**  
    * Columnas: `id`, `name`, `price`, `description`  
    * Fila 12: `12 | Bike | $800.00 | fixed gear, blue, fast`  
    * Fila 13: `13 | Helmet | $20.99 | black, fits most`  
    * Fila 14: `14 | Jersey | $35.00 | women's small, green and white`

---

### Diapositiva 8: La librería Dapper

* **Título principal:** La librería Dapper  
* **Texto de la diapositiva:**  
  * "**Dapper** es un MicroORM para .NET."  
  * "Similar a Entity Framework (otro ORM que tenemos disponible en .NET), va a ser una herramienta muy útil a la hora de realizar consultas a la base de datos."  
  * "Para poder invocar a los métodos de Dapper, es necesario agregar la directiva using al comienzo de la clase que lo va a utilizar:"  
    * **`using Dapper;`**  
  * "Una vez incorporada la librería a la clase, podremos utilizar los métodos que nos provee la misma (que se llevan muy bien con SQLClient, entre otros): **Query**, **QueryFirstOrDefault** y **Execute**."

---

### Diapositiva 9: ConnectionString

* **Título principal:** ConnectionString  
* **Texto de la diapositiva:**  
  * "La conexión con la base es costosa para un sistema o sitio web. Debemos tener en cuenta que solo debe haber 1 conexión activa a la base de datos a la vez."  
  * "Para conectarse a la base de datos es necesario definir un **ConnectionString**."  
  * "El ConnectionString para SQL Server se constituye de la siguiente forma:"  
    * **`private string _connectionString = @"Server=localhost;DataBase=NombreBase;Integrated Security=True;TrustServerCertificate=True;";`**  
  * "*Donde Server es el nombre de la terminal donde nos encontramos, DataBase es el nombre de la base de datos a la que nos estamos conectando, y los últimos 2 parámetros son necesarios para conectarnos mediante Windows Authentication.*"

---

### Diapositiva 10: Dapper \- Método Query

* **Título principal:** Dapper \- Método Query  
* **Texto de la diapositiva:**  
  * "Creamos un objeto de la clase Conexion (db) y luego hacemos una consulta (db.Query) cuyo resultado convertiremos a una lista de tipo Patente invocando a .ToList()."  
  * "Al encerrar la operación select en un bloque using, se cierra la conexión a la base."  
  * "Es MUY importante que las propiedades de la clase se llamen IGUAL que los campos que vengan de la query, y que tengan permisos de escritura (set)"  
* **Código C\#:**

28 public List\<Patente\> LevantarPatentes()

29 {

30     List\<Patente\> patentes \= new List\<Patente\>();

31     using(SqlConnection connection \= new SqlConnection(\_connectionString))

32     {

33         string query \= "SELECT \* FROM Patentes";

34         patentes \= connection.Query\<Patente\>(query).ToList();

35     }

36     return patentes;

37 }

3 public class Patente

4 {

5     public string Matricula { get; set; }

6     public DateTime FechaCompra { get; set; }

7 }

---

### Diapositiva 11: Dapper \- Método QueryFirstOrDefault

* **Título principal:** Dapper \- Método QueryFirstOrDefault  
* **Texto de la diapositiva:**  
  * "A veces, debemos realizar consultas a la base de datos que sabemos que devolverán sólo un registro."  
  * "En este caso, creamos un objeto de la clase Conexion (db) y luego haremos una consulta (db.QueryFirstOrDefault), cuyo resultado puede ser un objeto Patente, de encontrar la coincidencia definida en la cláusula WHERE, o null si no hay ninguna coincidencia"  
* **Código C\#:**

28 public Patente LevantarPatente(string patente)

29 {

30     Patente miPatente \= null;

31     using(SqlConnection connection \= new SqlConnection(\_connectionString))

32     {

33         string query \= "SELECT Matricula FROM Patentes WHERE Matricula \= @pPatente";

34         miPatente \= connection.QueryFirstOrDefault\<Patente\>(query, new { pPatente \= patente });

35     }

36     return miPatente;

37 }

---

### Diapositiva 12: Dapper \- Método Execute (INSERT)

* **Título principal:** Dapper \- Método Execute (INSERT)  
* **Texto de la diapositiva:**  
  * "El método **Execute** que nos provee la librería Dapper realiza la acción determinada y como resultado retorna la cantidad de registros que fueron afectados en la tabla de nuestra base de datos (un valor entero)."  
  * "Acá un ejemplo si quisiéramos agregar un jugador, recibimos por parámetro el objeto completo de la misma, y agregamos los parámetros individualmente."  
* **Código C\#:**

28 public void AgregarJugador(Jugador jug)

29 {

30     string query \= "INSERT INTO Jugadores (IdEquipo, Nombre, FechaNacimiento) VALUES (@pIdEquipo, @pNombre, @pFechaNacimiento)";

31     using(SqlConnection connection \= new SqlConnection(\_connectionString))

32     {

33         connection.Execute(query, new { pIdEquipo \= jug.IdEquipo, pNombre \= jug.Nombre, pFechaNacimiento \= jug.FechaNacimiento });

34     }

35 }

---

### Diapositiva 13: Dapper \- Método Execute (DELETE)

* **Título principal:** Dapper \- Método Execute (DELETE)  
* **Texto de la diapositiva:**  
  * "El método **Execute** que nos provee la librería Dapper realiza la acción determinada y como resultado retorna la cantidad de registros que fueron afectados en la tabla de nuestra base de datos (un valor entero)."  
  * "Acá un ejemplo si quisiéramos eliminar una patente, recibimos por parámetro el código de la misma, y procedemos a ejecutar la query típica de DELETE."  
* **Código C\#:**

28 public int EliminarPatente(string patenteAEliminar)

29 {

30     string query \= "DELETE FROM Patentes WHERE Matricula \= @patenteAEliminar";

31     int registrosAfectados \= 0;

32     using (SqlConnection connection \= new SqlConnection(\_connectionString))

33     {

34         registrosAfectados \= connection.Execute(query, new { patenteAEliminar });

35     }

36     return registrosAfectados;

37 }

---

### Diapositiva 14: Ejecutando Stored Procedures

* **Título principal:** Ejecutando Stored Procedures  
* **Texto de la diapositiva:**  
  * "Muchas veces se crean las consultas en la base de datos y se almacenan en stored procedures.."  
  * "El tercer parámetro del método Query indicará que en vez de recibir un sql se enviará un stored procedure."  
* **Código C\#:**

29 public List\<Patente\> LevantarPatentes(string letraInicial)

30 {

31     List\<Patente\> listaPatentes \= null;

32     using(SqlConnection connection \= new SqlConnection(\_connectionString))

33     {

34         string storedProcedure \= "TraerPatentesxLetra";

35         listaPatentes \=

36             connection.Query\<Patente\>(

37                 storedProcedure,

38                 new { Letra \= letraInicial },

39                 commandType: CommandType.StoredProcedure

40             ).ToList();

41     }

42     return listaPatentes;

43 }

* **Texto de pie de página:**  
  * "*Recordá incluir el using de la librería System.Data para que el código reconozca CommandType*"

---

### Diapositiva 15: Enlaces complementarios

* **Título principal:** Enlaces complementarios  
* **Enlaces listados:**  
  * `How Dapper Works?`  
  * `How to use the Dapper ORM in C#`  
  * `Introducción a Dapper | Conéctate a CUALQUIER Base de Datos en .NET | 4 Ejemplos`  
* **Texto explicativo:**  
  * "Podemos conectarnos a la base de datos utilizando el archivo de configuración del proyecto de Net Core de esta manera:"  
* **Enlace final:**  
  * `Store and read connection string in appsettings.json – ConnectionStrings.com`

**DIAPOSITIVA 1: Fundamentos de Sessions**

**DEFINICIÓN** Las Sessions proporcionan un mecanismo para almacenar información específica del usuario durante una sesión de navegación web.

Se basan en cookies de sesión que identifican al usuario en el servidor, permitiendo el acceso a datos almacenados.

---

**DIAPOSITIVA 2: Fundamentos de Sessions**

**CARACTERÍSTICAS**

* **Persistencia temporal:** Los datos de la sesión se mantienen mientras el usuario esté activo en la aplicación o hasta que la sesión expire.  
* **Almacenamiento en el servidor:** Los datos de sesión se almacenan en el servidor y se identifican mediante una cookie de sesión en el cliente.  
* **Acceso por clave-valor:** Se accede a los datos de sesión a través de claves únicas, similares a un diccionario.  
* **Configuración de tiempo de vida:** Se puede definir un tiempo de expiación automático o finalizar la sesión manualmente.

---

**DIAPOSITIVA 3: Implementación de Sessions**

**NUGET** Descargar los paquetes de NuGet: `dotnet add package Microsoft.AspNetCore.Session`

Contiene clases para manejar sesiones

**Program.cs** Debemos agregar líneas para indicarle a nuestro proyecto que utilizará Sessions.

Después de la línea `var builder = WebApplication.CreateBuilder(args);` : `builder.Services.AddSession();`

Y antes de `app.MapControllerRoute(...)` agregar: `app.UseSession();`

---

**DIAPOSITIVA 4: Implementación de Sessions**

**LISTO\!** Todo configurado\! Pero ahora... ¿Cómo guardamos una variable en session para después obtenerla en otra página?

`HttpContext.Session.SetString(Clave, valor);` `HttpContext.Session.GetString(Clave);`

Como ven, las valores en sesión se guardan en formato string, por lo que debemos enviar cada valor convirtiéndolo previamente a string\!

`HttpContext.Session.SetString("Nombre", nombreUsuario);` `HttpContext.Session.SetString("DNI", dni.ToString());`

`string Nombre = HttpContext.Session.GetString("Nombre");` `int dni = int.Parse(HttpContext.Session.GetString("DNI"));`

---

**DIAPOSITIVA 5: Implementación de Sessions**

**MÉTODOS PARA BORRAR DATOS DE SESSION**

// Eliminar una variable específica de sesión

Session.Remove("Usuario");

// Eliminar todas las variables de sesión

Session.Clear();

El método **Remove(key)** borra un dato específico de la sesión, mientras que **Clear()** borra todas las variables que haya en la sesión.

---

💡 Si te interesa practicar y consolidar estos conceptos, podemos diseñar un cuestionario interactivo para poner a prueba tus conocimientos sobre el manejo de sesiones en .NET.

### **https\://share.gemini.google/xgpS6QDnqdDQ**

### 

### **Diapositiva 1: Stored Procedures**

**Un procedimiento almacenado (stored procedure) funciona como una consulta predefinida guardada directamente en la base de datos que agrupa diversas instrucciones SQL.**

**Al asignarle un nombre a este conjunto de pasos, es posible reutilizarlo en cualquier momento sin necesidad de reescribir el código, lo que optimiza el tiempo y minimiza la posibilidad de errores. Además, permiten el uso de parámetros de entrada y salida, lo que otorga mayor flexibilidad y personalización a su ejecución.**

**Código de ejemplo (Izquierda \- Listar un cliente específico):**

CREATE PROCEDURE ListarUnCliente  
    @Id int  
AS  
BEGIN  
    \-- Insert statements for procedure here  
    SELECT \* FROM Clientes WHERE IdCliente \= @id  
END  
GO

**Código de ejemplo (Derecha \- Listar todos los clientes):**

CREATE PROCEDURE ListarClientes  
AS  
BEGIN  
    SELECT \* FROM Clientes  
END  
GO

---

### **Diapositiva 2: Stored Procedures \- ¿Donde se crean?**

En el explorador de objetos de **SQL Server Management Studio (SSMS)**, la ruta de creación sigue la estructura de carpetas de la base de datos:

* **BDEmpresa**  
  * **Programmability**  
    * **Stored Procedures**

Para crear uno nuevo, se hace **clic derecho sobre la carpeta "Stored Procedures"** y se selecciona:

* **New \-\> Stored Procedure...** o **Natively Compiled Stored Procedure...**

---

### **Diapositiva 3: Ejecución de un SP**

**Para probar un stored procedure, ingresa los siguientes comandos en el editor de consultas SQL:**

exec ListarClientes  
exec ListarUnCliente 1

**Ten en cuenta que el valor "1" se interpreta como el parámetro correspondiente al ID del cliente que deseas consultar.**

---

### **Diapositiva 4: Ejemplo de un SP con INSERT (En una tabla con ID autoincremental)**

**Código de creación del procedimiento:**

CREATE PROCEDURE InsertarNuevoCliente  
    @NombreCliente varchar(50),  
    @CUIT varchar(50),  
    @FotoCliente varchar(50),  
    @FechaUltimaCompra date,  
    @IdProvincia int  
AS  
BEGIN  
    INSERT INTO Clientes (NombreCliente, CUIT, FotoCliente, FechaUltimaCompra, IdProvincia)  
        VALUES (@NombreCliente, @CUIT, @FotoCliente, @FechaUltimaCompra, @IdProvincia)  
END  
GO

**Ejecución del Store Procedure:**

exec InsertarNuevoCliente 'Iara Jolo', '27-11111111-1', 'https\://linkedin/iara.jpg', '2025-06-12', 1

---

### **Diapositiva 5: Como modificar un SP**

En el explorador de objetos de **SSMS**, para modificar un procedimiento existente se hace **clic derecho sobre el nombre del SP** (bajo la carpeta *Stored Procedures*) y se selecciona la opción **Modify**.

**Esto nos mostrará un código similar a este:**

ALTER PROCEDURE \[dbo\].\[ListarClientes\]  
AS  
BEGIN  
    \-- Insert statements for procedure here  
    SELECT \* FROM Clientes  
END

**Notemos que dice ALTER PROCEDURE y NO CREATE PROCEDURE.**

---

### **Diapositiva 6: Algunas ventajas y formas de uso**

* **Reducción del tráfico de red:** Al ejecutarse como un único lote de comandos directamente en el servidor de base de datos, se minimiza la transferencia de datos entre el cliente y el servidor.  
* **Mayor seguridad:** Los procedimientos permiten que usuarios y aplicaciones realicen operaciones sobre objetos de la base de datos sin necesidad de otorgarles permisos directos sobre dichos objetos, restringiendo así el acceso innecesario.  
* **Reutilización de código:** Las operaciones redundantes se encapsulan en procedimientos. Esto elimina la necesidad de replicar código, disminuye las inconsistencias y permite que cualquier usuario o aplicación con los permisos adecuados reutilice la lógica implementada.

**Buenas prácticas: Nomenclatura** **Es fundamental asignar nombres descriptivos a los *stored procedures* que reflejen claramente la tarea que ejecutan, garantizando un entorno de trabajo organizado y fácil de mantener.**

### **Diapositiva 1: ¿Qué es un Join?**

**En SQL, un "join" es una operación que combina filas de dos o más tablas en función de una condición relacionada entre ellas.** Un join permite combinar datos de diferentes tablas en una sola consulta, lo que facilita la obtención de información relacionada almacenada en tablas separadas.

Hay varios tipos de joins en SQL, incluyendo:

1. **INNER JOIN**: Combina las filas de dos tablas basándose en una condición de igualdad entre las columnas especificadas en la cláusula JOIN. Solo se incluirán las filas que tengan coincidencias en ambas tablas.  
2. **LEFT JOIN**: Combina todas las filas de la tabla de la izquierda (tabla izquierda) con las filas coincidentes de la tabla de la derecha (tabla derecha) en función de la condición de igualdad especificada. Si no hay coincidencias en la tabla derecha, se rellenarán con valores NULL.  
3. **RIGHT JOIN**: Es similar al LEFT JOIN, pero combina todas las filas de la tabla de la derecha con las filas coincidentes de la tabla de la izquierda. Si no hay coincidencias en la tabla izquierda, se rellenarán con valores NULL.

---

### **Diapositiva 2: Inner Join (Parte 1\)**

**La cláusula "INNER JOIN" es utilizada para combinar las filas de dos o más tablas basándose en una condición de igualdad entre las columnas especificadas.**

**Ejemplo:** Tenemos dos tablas: "Clientes" y "Pedidos". La tabla "Clientes" contiene información sobre los clientes, como el nombre y la dirección, mientras que la tabla "Pedidos" contiene información sobre los pedidos realizados por los clientes, como el número de pedido y la fecha.

**Tabla "Clientes":**

* **ID\_cliente** \| **Nombre** \| **Dirección**  
* 1 \| Juan \| Calle Principal  
* 2 \| María \| Calle Secundaria  
* 3 \| Carlos \| Calle Principal

**Tabla "Pedidos":**

* **ID\_pedido** \| **Fecha** \| **ID\_cliente**  
* 1 \| 2023-05-01 \| 1  
* 2 \| 2023-05-02 \| 2  
* 3 \| 2023-05-03 \| 1  
* 4 \| 2023-05-04 \| 3

---

### **Diapositiva 3: Inner Join (Parte 2\)**

Si queremos obtener una lista de todos los pedidos junto con la información del cliente correspondiente, podemos usar un **"inner join"** para combinar las dos tablas en función de la columna "ID\_cliente". **La consulta SQL se vería así:**

SELECT Pedidos.ID\_pedido, Pedidos.Fecha, Clientes.Nombre, Clientes.Dirección  
FROM Pedidos  
INNER JOIN Clientes ON Pedidos.ID\_cliente \= Clientes.ID\_cliente;

**El resultado de la consulta sería el siguiente:**

* **ID\_pedido** \| **Fecha** \| **Nombre** \| **Dirección**  
* 1 \| 2023-05-01 \| Juan \| Calle Principal  
* 2 \| 2023-05-02 \| María \| Calle Secundaria  
* 3 \| 2023-05-03 \| Juan \| Calle Principal  
* 4 \| 2023-05-04 \| Carlos \| Calle Principal

**En resumen, un "INNER JOIN" en SQL combina filas de dos o más tablas en base a una condición de igualdad y devuelve sólo las filas que tienen coincidencias en ambas tablas.**

---

### **Diapositiva 4: Left Join**

**El "LEFT JOIN" es una operación de unión que combina todas las filas de la tabla de la izquierda (tabla izquierda) con las filas coincidentes de la tabla de la derecha (tabla derecha) en función de una condición de igualdad especificada.** La tabla de la izquierda es la tabla principal a la que se unen los datos de la tabla de la derecha.

**Ejemplo:** Seguimos trabajando con las tablas "Clientes" y "Pedidos". Pero esta vez queremos: **obtener una lista de todos los clientes junto con los pedidos realizados por cada cliente**

SELECT Clientes.Nombre, Pedidos.ID\_pedido, Pedidos.Fecha  
FROM Clientes  
LEFT JOIN Pedidos ON Clientes.ID\_cliente \= Pedidos.ID\_cliente;

**El resultado es el siguiente:**

* **Nombre** \| **ID\_pedido** \| **Fecha**  
* Juan \| 1 \| 2023-05-01  
* Juan \| 3 \| 2023-05-03  
* María \| 2 \| 2023-05-02  
* Carlos \| 4 \| 2023-05-04

**Nota:** Si un cliente no tiene ningún pedido registrado en la tabla "Pedidos", se mostrará un valor NULL en las columnas correspondientes a los pedidos.

---

### **Diapositiva 5: Right Join**

**El "RIGHT JOIN" es una operación de unión que combina todas las filas de la tabla de la derecha (tabla derecha) con las filas coincidentes de la tabla de la izquierda (tabla izquierda) en función de una condición de igualdad especificada.** La tabla de la derecha es la tabla principal a la que se unen los datos de la tabla de la izquierda.

**Ejemplo:** Seguimos trabajando con las tablas "Clientes" y "Pedidos". Pero esta vez queremos: **obtener una lista de todos los pedidos junto con la información del cliente correspondiente mostrando, el nombre del cliente, el id de pedido y la fecha del pedido**

SELECT Clientes.Nombre, Pedidos.ID\_pedido, Pedidos.Fecha  
FROM Clientes  
RIGHT JOIN Pedidos ON Clientes.ID\_cliente \= Pedidos.ID\_cliente;

**El resultado es el siguiente:**

* **Nombre** \| **ID\_pedido** \| **Fecha**  
* Juan \| 1 \| 2023-05-01  
* María \| 2 \| 2023-05-02  
* Juan \| 3 \| 2023-05-03  
* Carlos \| 4 \| 2023-05-04

**Nota:** Si un pedido no tiene ningún cliente registrado en la tabla "Pedidos", se mostrará un valor NULL en las columnas correspondientes a los pedidos.

**Captura de pantalla 2026-09-03 083847.png**

**LENGUAJE DE MANIPULACIÓN DE DATOS \- SQL** SQL (Structured Query Language o Lenguaje de Consulta Estructurada) es un lenguaje utilizado para **interactuar, gestionar y manipular** bases de datos relacionales. Permite organizar la información en tablas y ejecutar comandos para buscar datos, modificarlos, crear nuevo o estructurar una base de datos. Las instrucciones que aprenderemos de SQL se pueden diferenciar en dos categorías:

* **DML** \- Para interactuar con los datos  
* **DDL** \- Para interactuar con la estructura de la base de datos

---

**Captura de pantalla 2026-09-03 083850.png**

**LENGUAJE DE MANIPULACIÓN DE DATOS \[DML\]** Las sentencias de lenguaje de **manipulación** de datos son utilizadas para gestionar datos dentro de las tablas de una base de datos.

* **SELECT**: Para obtener como resultado un listado de datos de una tabla.  
* **INSERT**: Para insertar registros de datos a una tabla.  
* **UPDATE**: Para modificar datos de registros ya existentes dentro de una tabla.  
* **DELETE**: Elimina registros de una o varias tablas. **No reinicia los campos autonuméricos**.

---

**Captura de pantalla 2026-09-03 083854.png**

**La instrucción SELECT** Permite consultar los datos de una o más tablas. Dicha instrucción además se puede utilizar para filtrar los datos mostrados tanto a nivel atributos (campos) como a nivel de filas. **Estructura simple de un SELECT**: SELECT \[Campos\] FROM \[Tabla\] WHERE \[condiciones\] ORDER BY \[Campos\]

---

**Captura de pantalla 2026-09-03 083857.png**

**La instrucción SELECT** Veamos lo que escribimos antes, sentencia a sentencia...

* **SELECT** indica que inicia un comando de consulta de **selección**.  
* **FROM** se utiliza para indicar las tablas en las cuales se seleccionan los datos.  
* **WHERE** indica cuál o cuáles condiciones deben ocurrir para los datos solicitados (también puede pensarse como un filtro). Dentro de la cláusula WHERE se pueden usar operadores como AND, OR, BETWEEN, \<\>, \=, LIKE.  
* **ORDER BY** se indica según uno o varios campos, el orden con el que devolveremos los registros solicitados (Ascendente o Descendente).

---

**Captura de pantalla 2026-09-03 083901.png**

**La instrucción SELECT** **Ejemplos de Consultas SELECT**:

* SELECT \* FROM Clientes ORDER BY Mail;  
* SELECT Nombre, Precio FROM Pizzas ORDER BY Precio;  
* SELECT IdCliente, Fecha, Subtotal FROM Pedidos WHERE Subtotal \> 10000  
* SELECT \* FROM Pedidos WHERE Fecha \= '05-15-2026'  
* SELECT \* FROM Pizzas WHERE Precio \>8500 AND Precio\<10000  
* SELECT \* FROM PedidosPizzas WHERE IdPizza IN (4,5)

---

**Captura de pantalla 2026-09-03 083906.png**

**LIKE** El operador **LIKE** se usa en SQL para buscar patrones dentro de una columna de texto en consultas con **WHERE**. Se usa con el símbolo **%** (cualquier cantidad de caracteres). **Ejemplos de uso:** Buscar palabras que **comienzan** con un texto específico:

* SELECT \* FROM Clientes WHERE Mail LIKE 'juan%'; Buscar palabras que **terminan** con un texto específico:  
* SELECT \* FROM Clientes WHERE Mail LIKE '%gmail.com'; Buscar palabras que **contienen** un texto específico:  
* SELECT \* FROM Ingredientes WHERE Nombre LIKE '%ch%';

---

**Captura de pantalla 2026-09-03 083910.png**

**La instrucción INSERT** Se utiliza para insertar registros a una tabla. **Sintaxis**: INSERT INTO \[Tabla\] (\[Campo1\], \[Campo2\],...) VALUES (\[Valor1\], \[Valor2\],...); **Ejemplos:**

* INSERT INTO Ingredientes (Nombre) VALUES ('Huevo Frito');  
* INSERT INTO Clientes (mail, telefono) VALUES ('jesi@rodriguez.com', '1266342233') **Tips a tener en cuenta\!**  
* Las instrucciones INSERT **NO LLEVAN WHERE**  
* Los campos **AUTOINCREMENT** no deben completarse  
* **NO es obligatorio** insertar todos los campos

---

**Captura de pantalla 2026-09-03 083913.png**

**La instrucción UPDATE** Se utiliza para modificar (actualizar o cambiar) el valor o los valores de una fila o filas de una tabla. **Sintaxis**: UPDATE \[Tabla\] SET \[Campo1\]=\[Valor1\], \[Campo2\]=\[Valor2\] WHERE \[Condicion\] **Ejemplos**:

* UPDATE Clientes SET mail \= 'nuevomail@gmail.com' WHERE id= 2;  
* UPDATE Pizzas SET Precio \= Precio \* 1.10;  
* UPDATE Ingredientes SET Nombre='Muzzarella' WHERE Nombre='Margarita';  
* UPDATE Clientes SET telefono= '111111111', mail='leo@bbdd.com' WHERE Id=4

---

**Captura de pantalla 2026-09-03 083919.png**

**La instrucción DELETE** Permite eliminar una o más filas de una tabla. **Sintaxis**: DELETE FROM \[Tabla\] WHERE \[Condicion\]; **Ejemplos**:

* DELETE FROM Ingredientes WHERE Nombre='Anchoas';  
* DELETE FROM Clientes WHERE Mail LIKE 'V%';  
* DELETE FROM Pedidos; **Tips a tener en cuenta\!**  
* **Se borran registros completos\! NO campos.** Para borrar el valor de un campo utilizamos UPDATE.  
* **No te permitirá realizar deletes cuyo Id esté siendo utilizado en otra tabla como clave foránea\!**  
* **OJO QUE UN DELETE SIN WHERE BORRA TODA UNA TABLA\!**

### **ANSI SQL**

* **ANSI SQL** es el estándar oficial definido por el **American National Standards Institute**.  
* **SQL** significa **Structured Query Language (Lenguaje de Consultas Estructurado)**.  
* Gracias a las **consultas SQL**, podremos crear y administrar los objetos de nuestra base de datos:  
  * **Crear el esquema** de una base de datos.  
  * **Agregar, modificar, eliminar información** en las tablas (Manipulación de datos).  
  * **Consultar información** (Queries).

---

### **LENGUAJE DE DEFINICIÓN DE DATOS \[DDL\]**

Usamos DDL para **definir y modificar la estructura** de las tablas así como otros objetos de la base de datos.

* **CREATE**: Se utiliza para **crear objetos** (tablas, vistas, stores...) en una base de datos.  
* **ALTER**: **Modifica la estructura** de cualquier objeto de una base de datos.  
* **DROP**: Permite **borrar** cualquier objeto de una base de datos.  
* **TRUNCATE**: **Elimina todos los registros** de una tabla, incluyendo todos los espacios asignados a los registros. Además, **reinicia los campos autoincrementales**.

---

### **CREANDO UNA TABLA \[DDL\]**

Estructura sintáctica básica:

CREATE TABLE nombreDeTabla (

    nombreCampo1 tipoDeDatos \[DEFAULT valor\] \[restricciones\]

    \[, ...\]

);

* **nombreDeTabla**: Nombre de la tabla en el **DLR** creado anteriormente.  
* **nombreCampo1**: Nombre de un campo en la tabla.  
* **tipoDeDatos**: Tipo de datos del Campo a crear (referencia `*1`).  
* **`[DEFAULT valor]`**: Valor que toma el campo **por default** al crear un registro nuevo.  
* **`[restricciones]`**: Restricciones de Uso del campo (referencia `*2`).

---

### **CREACIÓN DE UNA TABLA CON INSTRUCCIONES SQL \[DDL\]**

**TIPOS DE DATO:**

* **VARCHAR (N)**: Texto, donde **N** es la cantidad máxima de caracteres.  
* **INT**: Número entero.  
* **BIT**: Equivale a **boolean**, puede tomar 2 estados (**1** es true, **0** false).  
* **CHAR**: 1 caracter alfanumérico.  
* **DOUBLE**: Número con decimales (el doble de un float).  
* **DATE**: Campo que almacena una fecha.

*(El texto menciona que hay más tipos que se verán más adelante).*

---

### **RESTRICCIONES EN LA CREACIÓN DE TABLAS**

Es donde indicamos qué **restricciones de datos** puede tener un campo:

* **PRIMARY KEY**: El campo es una **clave primaria**, no permite repetición.  
* **NOT NULL**: El campo **no permite valores nulos**.  
* **AUTOINCREMENT**: El campo se autocompleta con **números consecutivos** al crearse un nuevo registro.  
* **REFERENCES NombreTabla(NombreCampo) `[ParametrosRestriccion]`**: Indica que el campo es una **clave foránea** relacionada con una clave primaria de otra tabla (referencia `*3`).

---

### **PARÁMETROS DE REFERENCES PARA LA INTEGRIDAD**

Estructura de la restricción:

REFERENCES Alumnos(dni) \[ParametrosRestriccion\]

**`[ParametrosRestriccion]` disponibles:**

* **ON UPDATE**:  
  * **RESTRICT**  
  * **CASCADE**  
  * **NO ACTION**  
  * **SET NULL**  
* **ON DELETE**:  
  * **RESTRICT**  
  * **CASCADE**  
  * **NO ACTION**  
  * **SET NULL**

---

### **EJEMPLO DE CREACIÓN DE TABLA: NOTAS**

**Instrucción SQL de ejemplo:**

CREATE TABLE Notas(

    nota DOUBLE NOT NULL,

    trimestre INTEGER NOT NULL,

    dni INTEGER

        REFERENCES Alumnos(dni) ON UPDATE RESTRICT

                                ON DELETE SET NULL,

    codigoMateria INTEGER

);

**Esquema de Relación (DLR Notas):**

* **Tabla Alumnos**: Contiene los campos `*DNI int`, `Nombre Text` y `Curso Text`.  
* **Tabla Notas**: Contiene los campos `*Nota float`, `Trimestre int`, `dni int` y `CodigoMateria int`.  
* **Relación**: El campo `DNI` (lado 1\) de la tabla Alumnos se relaciona con el campo `dni` (lado muchos/infinito) de la tabla Notas.

---

### **MODIFICACIÓN DE UNA TABLA CON INSTRUCCIONES SQL \[DDL\]**

**Para agregar columnas:**

 ALTER TABLE nombreDeTabla

    ADD COLUMN nombreCampo1 tipoDeDatos

    \[FIRST\|AFTER NombreCampoexistente\];

* 

**Para eliminar columnas:**

 ALTER TABLE nombreDeTabla

    DROP COLUMN nombreCampoexistente1;

* 

**Para renombrar columnas:**

 ALTER TABLE nombreDeTabla

    RENAME COLUMN nombreCampoexistente1 nombrenuevoCampo;

* 

Aquí tienes el texto explícito y completo de todas las capturas de pantalla de tu libreta, estructurado para que sea fácil de leer en el chat:

### **1\. Introducción a la Normalización**

* **Título**: ¿Qué es *normalizar* y porqué es fundamental?  
* **Objetivo**: La normalización de bases de datos busca eliminar la redundancia de datos, mejorar la integridad de la información y optimizar el rendimiento de las operaciones.  
* **Beneficios**: Minimizar redundancia, mejorar la integridad de los datos, facilitar la actualización y eliminación de datos, mejorar la eficiencia de las consultas, y evitar errores de datos.  
* **Nota destacada**: Para normalizar una base de datos es necesario seguir unas Normas que garanticen su buen funcionamiento. Estos se llaman FORMAS NORMALES.

---

### **2\. Primera Forma Normal (1FN)**

* **Título**: Primera Forma *Normal*  
* **Definición**: Una base de datos se encuentra en la primera forma normal (1FN) cuando **no contiene grupos repetidos de datos**, es decir, cuando **cada columna contiene un único valor atómico** (indivisible).  
* **Ejemplo teórico**: Imagina una tabla de "Alumnos" que tenga una columna llamada "Amigos" y que dentro tenga los nombres de sus amigos separados por una coma. En 1FN esta columna debería separarse en una nueva tabla llamada "Amigos" con una relación a la tabla Alumnos.

#### **Caso Práctico 1FN (Ejemplo):**

* **Tabla: Alumnos (ejemplo sin 1FN)**:

   \| Nombre \| Email \| FechaNacimiento \| Amigos \| \| :--- \| :--- \| :--- \| :--- \| \| Lionel Messi \| lionel@messi.com \| 24/06/1988 \| De Paul, Martinez, Suarez, Alba, Paredes, Busquets \| \| Cristiano Ronaldo \| cristiano@ronaldo.com \| 7/12/1986 \| Benzema, Modric, Ramos \| \| Kilian Mbappe \| kilian@mbappe.com \| 5/1/1999 \| Griezmann, Madrid, Bellingham, Rudiger \|

* **Resolución**: Para aplicar 1FN debemos crear dos tablas: **Alumnos y Amigos**. Y luego crear una relación entre ambas tablas. ¿Cómo? Mediante la creación de **claves primarias**. ¿Qué es una clave primaria?

---

### **3\. Claves Primarias (Primary Key \- PK)**

* **Definición**: Una clave primaria es un campo o conjunto de campos en una tabla de base de datos que identifica de manera única cada fila (registro). Es un valor único, no nulo y no puede repetirse dentro de la tabla.

#### **Tabla Alumnos (con Clave Primaria):**

*La columna ID funciona como Clave Primaria (PK) de la tabla:*

| ID | Nombre | Email |
| ----- | ----- | ----- |
| **1** | Lionel Messi | lionel@messi.com |
| **2** | Cristiano Ronaldo | cristiano@ronaldo.com |
| **3** | Kilian Mbappe | kilian@mbappe.com |

El campo llamado **ID** contendrá valores que cumplen con las siguientes condiciones:

1. **Única**: No puede haber dos registros con el mismo valor en la clave primaria.  
2. **No nula**: No puede no contener valores.  
3. **Inmutable**: Su valor NO debe cambiar con el tiempo.

#### **Tabla Amigos (con Clave Primaria):**

Para la tabla **Amigos**, también debemos crear una clave primaria (PK):

| ID | Nombre |
| ----- | ----- |
| **1** | De Paul |
| **2** | Martinez |
| **3** | Suarez |
| **4** | Alba |
| **5** | Paredes |
| **6** | Busquets |
| **7** | Benzema |
| **8** | Modric |
| **9** | Ramos |
| **10** | Griezmann |
| **11** | Carvajal |
| **12** | Bellingham |
| **13** | Rudiger |

---

### **4\. Segunda Forma Normal (2FN)**

* **Título**: Segunda Forma *Normal*  
* **Definición**: Una base de datos cumple con la segunda forma normal (2FN) cuando está en 1FN y además cada columna no clave depende funcionalmente de la clave principal compuesta. La Segunda Forma Normal **solo existe para corregir problemas que ocurren cuando la PK es una clave compuesta por más de un campo**.  
* **Ejemplo teórico**: Si la clave principal de una tabla "Pedidos" es "ID" \+ "Cliente" (Clave Compuesta de dos campos) y la columna "Cliente" depende de la clave principal, pero la columna "Dirección" solo depende del "Cliente", se crea una nueva tabla "Clientes" con su "ID" como clave principal, el "Nombre". La "Dirección" también pasa a formar parte de esta nueva tabla.

#### **Caso Práctico 2FN (Ejemplo):**

* **ANTES (Tabla Pedidos original)**:

   \| ID \| Fecha \| Total \| Cliente \| Direccion \| \| :--- \| :--- \| :--- \| :--- \| :--- \| \| 1 \| 24/06/1988 \| 1500 \| Javier \| Yatay 240 \| \| 2 \| 7/12/1986 \| 2500 \| Martin \| Diaz Velez 4556 \| \| 3 \| 5/1/1999 \| 8999 \| David \| Rio de Janeiro 638 \|

* **DESPUÉS (Separación en dos tablas vinculadas)**:

   **Tabla Pedidos**:

   \| ID \| Fecha \| Total \| IDCliente \| \| :--- \| :--- \| :--- \| :--- \| \| 1 \| 24/06/1988 \| 1500 \| **1** \| \| 2 \| 7/12/1986 \| 2500 \| **2** \| \| 3 \| 5/1/1999 \| 8999 \| **3** \|

   **Tabla Clientes**:

   \| ID \| Nombre \| Direccion \| \| :--- \| :--- \| :--- \| \| **1** \| Javier \| Yatay 240 \| \| **2** \| Martin \| Diaz Velez 4556 \| \| **3** \| David \| Rio de Janeiro 638 \|

---

### **5\. Tercera Forma Normal (3FN)**

* **Título**: Tercera Forma *Normal*  
* **Definición**: Una base de datos se considera en la tercera forma normal (3FN) cuando está en 2FN y no existen dependencias transitivas. Es decir, **una columna no clave no puede depender funcionalmente de otra columna no clave**.

#### **Caso Práctico 3FN (Ejemplo):**

* **ANTES (Tabla Estudiantes original con dependencia transitiva entre Curso, Materia y Profesor)**:

   \| ID \| Nombre \| Curso \| Materia \| Profesor \| \| :--- \| :--- \| :--- \| :--- \| :--- \| \| 1 \| Uriel H \| NI4E \| Base de Datos \| Leo Kristal \| \| 2 \| Eliel J \| NI4E \| PRG 4 \| Ezequiel Binker \| \| 3 \| David J \| NI4A \| PRG 4 \| Iara Jolodovsky \|

* **DESPUÉS (Separación en cuatro tablas normalizadas)**:

   **Tabla Estudiantes**:

   \| ID \| Nombre \| \| :--- \| :--- \| \| **1** \| Uriel H \| \| **2** \| Eliel J \| \| **3** \| David J \|

   **Tabla Materias**:

   \| ID \| Materia \| \| :--- \| :--- \| \| **1** \| Base de Datos \| \| **2** \| PRG 4 \|

   **Tabla Profesores**:

   \| ID \| Profesor \| \| :--- \| :--- \| \| **1** \| Leo Kristal \| \| **2** \| Ezequiel Binker \| \| **3** \| Iara Jolodovsky \|

   **Tabla Cursos**:

   \| Curso \| IDEstudiante \| IdMateria \| IDProfesor \| \| :--- \| :--- \| :--- \| :--- \| \| NI4E \| 1 \| 1 \| 1 \| \| NI4E \| 2 \| 2 \| 2 \| \| NI4A \| 3 \| 2 \| 3 \|

Aquí tienes el texto explícito de cada una de las imágenes de tus fuentes:

### **Captura de pantalla 1: Funciones de Agregación**

* **COUNT()**: Cuenta el número de filas en una consulta.  
  * *Ejemplo:* `SELECT COUNT(*) FROM clientes;`  
* **SUM()**: Suma los valores de una columna numérica.  
  * *Ejemplo:* `SELECT SUM(ventas) FROM ordenes;`  
* **AVG()**: Calcula el promedio de los valores de una columna.  
  * *Ejemplo:* `SELECT AVG(precio) FROM productos;`  
* **MIN() y MAX()**: Encuentra el valor mínimo o máximo absoluto.  
  * *Ejemplos:*  
    * Fecha de nacimiento más antigua.  
    * Salario más alto.  
* *Nota al pie:* Estas funciones permiten obtener información resumida de los datos de manera eficiente.

---

### **Captura de pantalla 2: GROUP BY**

* **Agrupación**: Agrupa filas con los mismos valores en una o más columnas.  
* **Funciones de Agregación**: Se utiliza con funciones de agregación para calcular estadísticas por grupo.  
* *Ejemplo:* `SELECT categoria, SUM(cantidad) FROM Ventas GROUP BY categoria;`  
* *Nota al pie:* Permite analizar datos por categorías y obtener información específica para cada grupo.

---

### **Captura de pantalla 3: ¿Qué columnas puedo incluir en el SELECT?**

* Al agrupar, el resultado colapsa muchas filas en una sola por cada grupo.  
* **Regla**: Solo puedes incluir en el SELECT las columnas que estén en el GROUP BY o dentro de una función de agregación.  
* **Ejemplo de ERROR común**: `SELECT departamento, nombre, AVG(salario) FROM Empleados GROUP BY departamento;`  
* **¿Por qué falla?**: Porque en un mismo departamento hay muchos nombres. SQL no sabe cuál de todos mostrar en la única fila del grupo.

---

### **Captura de pantalla 4: HAVING: Filtrado de Grupos**

1. **Filtrado de Grupos**: Filtra los resultados de un GROUP BY basado en una condición.  
2. **Diferencia con WHERE**: WHERE filtra filas individuales; HAVING filtra grupos.  
3. **Ejemplo**: `SELECT departamento, AVG(salario) FROM Empleados GROUP BY departamento HAVING AVG(salario) > 50000;`  
* *Nota al pie:* Permite refinar los resultados de la agrupación y obtener información más precisa.

---

### **Captura de pantalla 5: WHERE vs. HAVING**

1. **WHERE**: Filtra empleados individuales antes de calcular promedios.  
2. **HAVING**: Filtra departamentos enteros después de calcular el promedio.

**Ejemplo de código**:  
 SELECT departamento, AVG(salario)

FROM Empleados

WHERE estado\_civil \= 'Soltero' \-- Filtra personas antes de agrupar

GROUP BY departamento

HAVING AVG(salario) \> 50000; \-- Filtra grupos ya calculados

* 

---

### **Captura de pantalla 6: Orden de Ejecución en SQL**

* **FROM / JOIN**: Primero el motor identifica de dónde vienen los datos.  
* **WHERE**: Filtra las filas individuales antes de cualquier cálculo.  
* **GROUP BY**: Organiza las filas sobrevivientes en grupos.  
* **HAVING**: Filtra esos "baldes" ya calculados.  
* **ORDER BY**: Organiza el resultado final para la vista del usuario

### **1\. Stored Procedures: Introducción y Sintaxis**

La presentación explica que los **procedimientos almacenados** pueden recibir datos y devolver información utilizando parámetros de entrada y salida, respectivamente.

La **sintaxis básica** para definirlos es la siguiente:

CREATE PROCEDURE \<Nombre\_Del\_Procedure\>  
    @param1 tipoDeDato \= ValorDefault  
AS  
BEGIN  
    // Aca va la instrucción SQL  
END

---

### **2\. Ejemplo con un Parámetro**

Muestra cómo crear un procedimiento llamado `sp_GustosxHeladeria` que recibe el parámetro `@idHeladeria` de tipo entero (`int`) para filtrar los gustos:

CREATE PROCEDURE sp\_GustosxHeladeria  
    @idHeladeria int  
AS  
BEGIN  
    SELECT \* FROM GustosHeladeria  
    WHERE IdHeladeria \= @idHeladeria  
END

---

### **3\. Ejecución de un SP**

Para probar o ejecutar el stored procedure anterior, la sintaxis requerida es:

exec sp\_GustosxHeladeria 1

El número `1` es el valor que SQL interpretará como el ID de la heladería que el usuario desea elegir.

---

### **4\. IF EXISTS o IF NOT EXISTS en SQL**

Estas son condiciones utilizadas en SQL para verificar si un registro o un objeto (como una tabla o procedimiento) existe antes de realizar una operación. Se combinan comúnmente con comandos como `DELETE`, `INSERT`, `DROP`, entre otros.

---

### **5\. Utilidad de IF EXISTS y IF NOT EXISTS**

* **Propósito general:** Sirve para verificar si un dato ya existe en nuestra tabla.  
* **IF EXISTS:** Ejecuta la acción correspondiente únicamente si el registro existe.  
* **IF NOT EXISTS:** Ejecuta la acción sólo si el registro no existe.

---

### **6\. Ejemplo de SP con INSERT**

Presenta un ejemplo aplicado a una tabla con ID autoincrementable para evitar insertar duplicados utilizando `IF NOT EXISTS`:

CREATE PROCEDURE sp\_InsertarGusto  
    @gusto NVARCHAR(50)  
AS  
BEGIN  
    /\* Comienzo del SP \*/  
    IF NOT EXISTS (SELECT \* FROM GustosHeladeria WHERE IdGusto \= @gusto)  
    BEGIN  
        /\* Comienzo IF \*/  
        INSERT INTO GustosHeladeria (IdGusto)  
        VALUES (@gusto)  
    END /\* Fin If \*/  
END /\* Fin SP \*/

---

### **7\. Ejemplo de SP con DELETE**

Muestra cómo eliminar un registro de forma segura verificando previamente si existe mediante `IF EXISTS`:

CREATE PROCEDURE sp\_EliminarGusto  
    @gusto NVARCHAR(50)  
AS  
BEGIN  
    IF EXISTS (SELECT \* FROM GustosHeladeria WHERE IdGusto \= @gusto)  
    BEGIN  
        DELETE FROM GustosHeladeria  
        WHERE IdGusto \= @gusto  
    END  
END

---

### **8 y 9\. Ejemplo con INSERT o UPDATE**

Este procedimiento está diseñado para insertar un nuevo gusto en la tabla, pero si el dato ya existe, lo modifica (actualiza).

El **código** del procedimiento `sp_InsertarOModificarGusto` es:

CREATE PROCEDURE sp\_InsertarOModificarGusto  
    @gusto NVARCHAR(50),  
    @cantidad int  
AS  
BEGIN  
    IF NOT EXISTS (SELECT \* FROM GustosHeladeria WHERE IdGusto \= @gusto)  
    BEGIN  
        INSERT INTO GustosHeladeria (IdGusto, Cantidad) VALUES (@gusto, @cantidad);  
    END  
    ELSE  
    BEGIN  
        UPDATE GustosHeladeria SET Cantidad \= @cantidad WHERE IdGusto=@gusto;  
    END  
END

---

### **10 y 11\. Ejemplo con Devolución de un Valor (RETURN)**

Este procedimiento agrega cantidades a ciertos gustos de helados y, tras actualizarlos, indica a cuántos registros modificó utilizando `RETURN`.

El **código** del procedimiento `ActualizarCantidades` es:

CREATE PROCEDURE ActualizarCantidades  
    @cantidad INT  
AS  
BEGIN  
    UPDATE GustosHeladeria  
    SET Cantidad \= Cantidad \+ @cantidad  
    WHERE IdGusto \> 10;

    RETURN @@ROWCOUNT; \-- Devuelve la cantidad de filas afectadas por el UPDATE  
END

---

### **12\. Variables en Stored Procedures**

Explica cómo declarar y asignar variables internas:

* **Declaración:** `DECLARE @cantidad INT;`  
* **Asignación:** `SET @cantidad = 5;`

**Diferencia clave:** Las variables se escriben con `@` al igual que los parámetros, pero se declaran y asignan dentro del procedimiento (después del `BEGIN`), mientras que los parámetros se definen antes.

**Caso práctico propuesto:** En una tabla de 16 heladerías donde cada una comercializa distintos gustos, se busca crear un procedimiento que cuente la cantidad de gustos acumulados en 2 heladerías elegidas por el usuario.

---

### **13\. Ejemplo de Variables y Consulta (CantidadGustosPorSucursal)**

Este ejemplo práctico muestra cómo usar variables para acumular resultados de consultas y devolverlos:

CREATE PROCEDURE CantidadGustosPorSucursal  
    @Sucursal1 INT,  
    @Sucursal2 INT  
AS  
BEGIN  
    DECLARE @CantidadGustos INT; \-- Creo e inicializo la variable  
    SET @CantidadGustos \= 0;

    \-- Cantidad de gustos de la Sucursal 1  
    SELECT @CantidadGustos \+= COUNT(\*) FROM GustosHeladeria  
    WHERE IdHeladeria \= @Sucursal1; \-- Le asigno lo que devuelve el 1er count

    \-- Cantidad de gustos de la Sucursal 2  
    SELECT @CantidadGustos \+= COUNT(\*) FROM GustosHeladeria  
    WHERE IdHeladeria \= @Sucursal2; \-- Le asigno lo que devuelve el 2do count

    SELECT @CantidadGustos; \-- El procedimiento devuelve lo que tiene la variable  
END;

---

### **14\. Ejercicio Propuesto**

La presentación plantea un ejercicio práctico:

> Dada una tabla de **Usuarios** con los campos **User** y **Pass**, crear un stored procedure que reciba 2 parámetros (`user` y `pass`) y verifique si el usuario está en la base de datos. En caso de estar, debe retornar un `1`, de lo contrario, un `0`.

---

### **15\. Algunas Ventajas de Uso de los SP**

* **Tráfico de red reducido entre cliente y servidor:** Los comandos del procedimiento se ejecutan en un único lote de código.  
* **Mayor seguridad:** Permite que varios usuarios y programas ejecuten operaciones sobre los objetos de la base de datos a través del procedimiento, incluso si no disponen de permisos directos sobre dichos objetos.  
* **Reutilización de código:** Las operaciones de base de datos redundantes se encapsulan en procedimientos, lo que elimina la necesidad de volver a escribir el código, reduce inconsistencias y facilita el acceso a cualquier usuario con permisos suficientes.

* Primero dividimos las páginas en distintos htmls y un solo css. Dividimos cada html en head, title, body, header (Contiene al encabezado de una sección o documento, donde se colocan habitualmente vínculos de navegación, los formularios de búsqueda, etc. Sus contenidos son consistentes a lo largo de todo el sitio.), main (Representa el contenido principal del \<body\> de un documento o aplicación. El área principal del contenido consiste en el contenido que está directamente relacionado, o se expande sobre el tema central de un documento o la funcionalidad central de una aplicación.), section(  
  Es un elemento de seccionamiento genérico, diseñado  
  para  
  contener  
  temáticamente  
  una parte de un documento definida. Los contenidos de un  
  elemento section son usualmente precedidos por un encabezado.  
* ), div(  
  El elemento \<div\> es un contenedor genérico que agrupa y estructura dentro de una página, sin aportar significado semántico, utilizado principalmente para aplicar estilos o manipulación mediante CSS y JavaScript.  
* ),article(  
  El elemento \<article\> en HTML define un contenido auto-contenido y reutilizable, como artículos, entradas de blog, comentarios o widgets, permitiendo su distribución independiente.  
* ),  h1,p, pre, etc. y footer(  
  Representa al pie de una sección o documento, donde los autores habitualmente colocan firmas, información acerca del autor, etc. Son consistentes a lo largo de todo el sitio.  
* ).   
* src y alt para img  
* para css ():   
* color: red, \#ffffff, rgb y rgba, lo q quieras  
* font-size:em, large, larger, px  
  font-weight: bold; text-decoration: underline;  
  text-align: center;  
  text-transform: uppercase; text-shadow: \#7f8c8d;  
* font-family  
* margin (top, bottom, left right o normal)  
* padding (igual q margin)  
* border radius  
* ejemplo: border: 1px(size) solid(style) black(color), se puede border right, top, left y bottom tambien  
* button en html  
* en css, se puede ajustar el tamaño usando width (px y %) y height (px, % y auto)  
* a href  
* en css se pueden hacer selectores, es decir, \*, ej: div h3, ej: h3,h4. ademas se puede hacer selectores con id \= “xx” y luego en css llamarlo con \#.  
* se puede usar form, con los atributos method (get,post) y action(). en form usamos input, ej:   
* \<form method="post" action="gracias.html"\>  
* \<input type="email" /\>  
* \<input type="tel" /\> \<input type="date" /\>  
* \<input type="url" /\>  
* \<input type="color" /\>  
* \<input type="search" /\>  
* \<input  
* type="number" max="999" min="0" step="1" /\>  
* \<input type="range" max="999" min="0" step="1" /\> \</form\>  
* tambien se puede usar input type checkbox, radio y select. ademas claro que se usa reset y submit y placeholder  
* se pueden usar listas (ul y li, tambien ol). se puede, en css, usar list syle type none.  
* se puede usar float right y left al igual que clear   
* background se puede usar todo esto en css:   
  background-color: \#2C3E50;  
  background-image: url("imagen.jpg");  
  background-repeat: no-repeat;  
  background-position: center;  
  background-size: cover;  
  background-attachment: fixed;  
* navegadores (  
  El elemento HTML \<nav\> representa una sección de una página cuyo propósito es proporcionar enlaces de navegación, ya sea dentro del documento actual o a otros documentos. Ejemplos comunes de secciones de navegación son menús, tablas de contenido e índices. ) lo usamos con ul li a mayoritariamente.  
* se pueden usar clases, sin abusar de ellas. (  
  Tiene un nivel de detalle mayor. Sirve para aplicar un estilo a un conjunto de componentes de un mismo tipo (o que sirvan para un mismo propósito) pero de forma diferenciada al selector de tipo.  
  Por ejemplo la clase .banners podría hacer referencia a todos los img de mi sitio web que tengan relación con una tarjeta de perfil de una persona.)  
* tambien se pueden utilizar selectores de atributo ejemplo: /\* Elementos \<a\> con un atributo title \*/  
* a\[title\] {  
*   color: purple;  
* }  
*   
* /\* Elementos \<a\> con un href que coincida con "https\://example.org" \*/  
* a\[href="https\://example.org"\] {  
*   color: green;  
* }  
*   
* /\* Elementos \<a\> con un href que contenga "example" \*/  
* a\[href\*="example"\] {  
*   font-size: 2em;  
* }  
*   
* /\* Elementos \<a\> con un href que comience con "\#" \*/  
* a\[href^="\#"\] {  
*   color: \#001978;  
* }  
*   
* /\* Elementos \<a\> con un href que termine en ".org" \*/  
* a\[href\$=".org"\] {  
*   font-style: italic;  
* }  
*   
* /\* Elementos \<a\> cuyo atributo class contenga la palabra "logo" \*/  
* a\[class\~="logo"\] {  
*   padding: 2px;  
* }  
* NO ABUSAR DE BR  
* iframe: lo podemos usar con google maps o video de yt  
* se pueden usar pseudo clases (first child, last child, hover)  
*   
  ¿Qué son las jerarquías en CSS?  
  En CSS, la jerarquía de estilo determina qué se aplican a un elemento cuando existen múltiples reglas en conflicto. La prioridad se establece de acuerdo a la especificidad del selector y el orden en que se declaran las reglas.  
  Es clave el orden y seguir un patrón en el desarrollo de sus proyectos para llegar a un estilo logrado.  
* podemos usar display:   
  La propiedad display en CSS define cómo se comporta un elemento en la estructura de la página. Determina si se muestra en línea con otros elementos (inline), si ocupa todo el ancho disponible en una nueva línea (block), o si combina características de ambos (inline-block). Es fundamental para controlar el diseño y la distribución del contenido en la web.  
* grid:   
  Permite organizar elementos en áreas o regiones dentro de un contenedor.  
  Podemos definir  
  estructuras  
  bidimensionales  
  precisas,  
  controlando tanto la alineación horizontal como vertical de los  
  elementos. Esto facilita la creación de diseños complejos y adaptables, ya que permite dividir el espacio de manera clara y ordenada. lo podemos usar display: grid, grid template columns: 1fr,1fr,1fr,  gap: , width y margin  
* position: podemos usarlo con position: absolute, relative  
* podemos usar display: flex, justify content: , align items:  
* podemos usar text align tambien:

## gpt

# **Guía de HTML y CSS permitidos**

## **1\. Estructura general del proyecto**

* Primero dividimos las páginas en distintos HTMLs y un solo CSS.

### **Estructura básica de cada HTML**

* `head`  
* `title`  
* `body`

### **Elementos semánticos**

#### **`header`**	

Contiene al encabezado de una sección o documento, donde se colocan habitualmente vínculos de navegación, los formularios de búsqueda, etc. Sus contenidos son consistentes a lo largo de todo el sitio.

#### **`main`**

Representa el contenido principal del `<body>` de un documento o aplicación. El área principal del contenido consiste en el contenido que está directamente relacionado, o se expande sobre el tema central de un documento o la funcionalidad central de una aplicación.

#### **`section`**

Es un elemento de seccionamiento genérico, diseñado para contener temáticamente una parte de un documento definida. Los contenidos de un elemento `section` son usualmente precedidos por un encabezado.

#### **`article`**

El elemento `<article>` en HTML define un contenido auto-contenido y reutilizable, como artículos, entradas de blog, comentarios o widgets, permitiendo su distribución independiente.

#### **`div`**

El elemento `<div>` es un contenedor genérico que agrupa y estructura dentro de una página, sin aportar significado semántico, utilizado principalmente para aplicar estilos o manipulación mediante CSS y JavaScript.

#### **`footer`**

Representa al pie de una sección o documento, donde los autores habitualmente colocan firmas, información acerca del autor, etc. Son consistentes a lo largo de todo el sitio.

### **Otros elementos HTML**

* `h1`  
* `p`  
* `pre`  
* etc.

---

# **2\. Imágenes**

Utilizar:

\<img src="imagen.jpg" alt="Descripción"\>

* `src`  
* `alt`

---

# **3\. Enlaces**

### **Etiqueta `a`**

\<a href="pagina.html"\>Ir a la página\</a\>

* `href`

---

# **4\. Botones**

\<button\>Enviar\</button\>  
---

# **5\. Formularios**

Se puede usar `form` con los atributos:

* `method`  
  * `get`  
  * `post`  
* `action`

Ejemplo:

\<form method="post" action="gracias.html"\>

   \<input type="email" /\>  
   \<input type="tel" /\>  
   \<input type="date" /\>  
   \<input type="url" /\>  
   \<input type="color" /\>  
   \<input type="search" /\>

   \<input  
   type="number"  
   max="999"  
   min="0"  
   step="1" /\>

   \<input  
   type="range"  
   max="999"  
   min="0"  
   step="1" /\>

\</form\>

También se puede usar:

* `checkbox`  
* `radio`  
* `select`  
* `reset`  
* `submit`  
* `placeholder`

---

# **6\. Listas**

### **No ordenadas**

\<ul\>  
   \<li\>Elemento\</li\>  
\</ul\>

### **Ordenadas**

\<ol\>  
   \<li\>Elemento\</li\>  
\</ol\>

En CSS:

list-style-type: none;  
---

# **7\. Navegación**

### **`nav`**

El elemento HTML `<nav>` representa una sección de una página cuyo propósito es proporcionar enlaces de navegación, ya sea dentro del documento actual o a otros documentos.

Ejemplos comunes de secciones de navegación son:

* menús  
* tablas de contenido  
* índices

Normalmente lo usamos junto con:

\<nav\>  
   \<ul\>  
       \<li\>\<a href=""\>Inicio\</a\>\</li\>  
   \</ul\>  
\</nav\>  
---

# **8\. Iframes**

Se pueden usar para:

* Google Maps  
* Videos de YouTube

---

# **9\. Clases e IDs**

## **Clases**

Se pueden utilizar clases, sin abusar de ellas.

Tiene un nivel de detalle mayor. Sirve para aplicar un estilo a un conjunto de componentes de un mismo tipo (o que sirvan para un mismo propósito) pero de forma diferenciada al selector de tipo.

Por ejemplo:

.banners

Podría hacer referencia a todos los `img` de mi sitio web que tengan relación con una tarjeta de perfil de una persona.

## **IDs**

HTML:

id="xx"

CSS:

\#xx  
---

# **10\. CSS \- Propiedades de texto**

## **Colores**

color: red;  
color: \#ffffff;  
color: rgb(...);  
color: rgba(...);

## **Tamaño de fuente**

font-size: em;  
font-size: large;  
font-size: larger;  
font-size: px;

## **Peso**

font-weight: bold;

## **Decoración**

text-decoration: underline;

## **Alineación**

text-align: center;

También podemos usar `text-align`.

## **Transformación**

text-transform: uppercase;

## **Sombra**

text-shadow: \#7f8c8d;

## **Fuente**

font-family: ...  
---

# **11\. CSS \- Espaciado**

## **Margin**

margin  
margin-top  
margin-bottom  
margin-left  
margin-right

## **Padding**

padding  
padding-top  
padding-bottom  
padding-left  
padding-right  
---

# **12\. CSS \- Bordes**

## **Border radius**

border-radius: ...

## **Border**

Ejemplo:

border: 1px solid black;

Donde:

* `1px` \= size  
* `solid` \= style  
* `black` \= color

También:

border-top  
border-right  
border-bottom  
border-left  
---

# **13\. CSS \- Tamaños**

width  
height

### **Width**

* px  
* %

### **Height**

* px  
* %  
* auto

---

# **14\. CSS \- Fondos**

background-color: \#2C3E50;  
background-image: url("imagen.jpg");  
background-repeat: no-repeat;  
background-position: center;  
background-size: cover;  
background-attachment: fixed;  
---

# **15\. Selectores CSS**

## **Selector universal**

\*

## **Selector por etiqueta**

p  
div  
h1

## **Selector descendente**

div h3

## **Selector múltiple**

h3, h4

## **Selector por ID**

\#miId  
---

# **16\. Selectores de atributo**

### **Elementos `<a>` con atributo title**

a\[title\] {  
   color: purple;  
}

### **href exacto**

a\[href="https\://example.org"\] {  
   color: green;  
}

### **href que contiene texto**

a\[href\*="example"\] {  
   font-size: 2em;  
}

### **href que comienza con**

a\[href^="\#"\] {  
   color: \#001978;  
}

### **href que termina con**

a\[href\$=".org"\] {  
   font-style: italic;  
}

### **class que contiene una palabra**

a\[class\~="logo"\] {  
   padding: 2px;  
}  
---

# **17\. Pseudoclases**

Se pueden usar:

:first-child  
:last-child  
:hover  
---

# **18\. Float**

float: left;  
float: right;

También:

clear  
---

# **19\. Display**

La propiedad `display` en CSS define cómo se comporta un elemento en la estructura de la página.

Determina si se muestra:

* en línea con otros elementos (`inline`)  
* ocupando todo el ancho disponible (`block`)  
* combinando características de ambos (`inline-block`)

Es fundamental para controlar el diseño y la distribución del contenido en la web.

---

# **20\. Flexbox**

Podemos usar:

display: flex;  
justify-content: ...;  
align-items: ...;  
---

# **21\. Grid**

Permite organizar elementos en áreas o regiones dentro de un contenedor.

Podemos definir estructuras bidimensionales precisas, controlando tanto la alineación horizontal como vertical de los elementos.

Facilita la creación de diseños complejos y adaptables.

Ejemplos:

display: grid;  
grid-template-columns: 1fr 1fr 1fr;  
gap: ...;  
width: ...;  
margin: ...;  
---

# **22\. Position**

Podemos usar:

position: relative;  
position: absolute;  
---

# **23\. Jerarquía y especificidad CSS**

## **¿Qué son las jerarquías en CSS?**

En CSS, la jerarquía de estilo determina qué reglas se aplican a un elemento cuando existen múltiples reglas en conflicto.

La prioridad se establece de acuerdo a:

* la especificidad del selector  
* el orden en que se declaran las reglas

Es clave el orden y seguir un patrón en el desarrollo de sus proyectos para llegar a un estilo logrado.

### **Orden de prioridad**

1. `!important`  
2. `ID`  
3. `Atributo`  
4. `Clase`  
5. `Etiqueta`  
6. `*` (selector universal)

---

# **24\. Estructura de una regla CSS**

Ejemplo:

p {  
   color: red;  
}

* `p` → Selector  
* `color` → Propiedad  
* `red` → Valor de la propiedad  
* `color: red;` → Declaración

---

# **25\. Buenas prácticas**

* NO ABUSAR DE `br`.  
* Se pueden usar clases, pero sin abusar de ellas.  
* Es clave respetar la jerarquía y especificidad de CSS.  
* Seguir un patrón ordenado en el desarrollo del proyecto.

