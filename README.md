## Fantasy Life I Wiki API (FlikiAPI)

Fliki is a RESTful API designed to provide access to the data and resources of the Fantasy Life I game. 
It allows developers to retrieve information about game elements such as items, quests, and characters in a structured format.
It converts the complex data structures of the game into a more accessible format for developers and users.

## Endpoints

### Get Entry by ID

- **URL**: `/api/id/{id}`
- **Method**: `GET`
- **Description**: Retrieve an entry by its ID.
- **Response**: Returns the entry data and any back-references.

### Get Entry by Type and ID

- **URL**: `/api/{type}/{id}`
- **Method**: `GET`
- **Description**: Retrieve an entry by its type and ID.
- **Response**: Returns the entry data and any back-references.
