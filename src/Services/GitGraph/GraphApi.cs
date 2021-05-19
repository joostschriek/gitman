public abstract class GitGraph {
    public void Get() {

    }
}

// {
//   repository(owner: "sectigo-eng", name: "ca-service") {
//     collaborators {
//       edges {
//         permission
//         node {
//           email
//           id
//           login
//         }
//         permissionSources {
//           permission
//           source {
//             ... on Team {
//               resourcePath
//             }
//             ... on Repository {
//               resourcePath
//             }
//           }
//         }
//       }
//       pageInfo {
//         startCursor
//         hasNextPage
//         endCursor
//       }
//       totalCount
//     }
//   }
// }
